using BuilderCore.Web.Models;
using Dapper;
using Microsoft.Data.Sqlite;

namespace BuilderCore.Web.Data.Repositories;

public sealed class JobCostRepository(ISqliteConnectionFactory connectionFactory)
{
    public async Task<IReadOnlyList<JobCostTransaction>> GetTransactionsAsync(int jobId, CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<JobCostTransactionRow>(
            new CommandDefinition(
                """
                SELECT jct.JobCostTransactionId, jct.JobId, jct.CostCodeId, cc.Code AS CostCode,
                       cc.Description AS CostCodeDescription, jct.VendorId, v.VendorName,
                       jct.PurchaseOrderId, po.PONumber, jct.ReferenceNumber, jct.Description,
                       jct.TransactionDate, jct.Amount
                FROM JobCostTransaction jct
                INNER JOIN CostCode cc ON cc.CostCodeId = jct.CostCodeId
                LEFT JOIN Vendor v ON v.VendorId = jct.VendorId
                LEFT JOIN PurchaseOrder po ON po.PurchaseOrderId = jct.PurchaseOrderId
                WHERE jct.JobId = @JobId
                ORDER BY jct.TransactionDate DESC, jct.JobCostTransactionId DESC;
                """,
                new { JobId = jobId },
                cancellationToken: cancellationToken));

        return rows.Select(r => new JobCostTransaction
        {
            JobCostTransactionId = r.JobCostTransactionId,
            JobId = r.JobId,
            CostCodeId = r.CostCodeId,
            CostCode = r.CostCode,
            CostCodeDescription = r.CostCodeDescription,
            VendorId = r.VendorId,
            VendorName = r.VendorName,
            PurchaseOrderId = r.PurchaseOrderId,
            PONumber = r.PONumber,
            ReferenceNumber = r.ReferenceNumber,
            Description = r.Description,
            TransactionDate = DateOnly.Parse(r.TransactionDate),
            Amount = r.Amount
        }).ToList();
    }

    public async Task<int> CreateTransactionAsync(JobCostTransaction transaction, string userId, CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(
                """
                INSERT INTO JobCostTransaction
                    (JobId, CostCodeId, VendorId, PurchaseOrderId, ReferenceNumber, Description,
                     TransactionDate, Amount, CreatedBy, CreatedDateUtc)
                VALUES
                    (@JobId, @CostCodeId, @VendorId, @PurchaseOrderId, @ReferenceNumber, @Description,
                     @TransactionDate, @Amount, @CreatedBy, @CreatedDateUtc);
                SELECT last_insert_rowid();
                """,
                new
                {
                    transaction.JobId,
                    transaction.CostCodeId,
                    transaction.VendorId,
                    transaction.PurchaseOrderId,
                    transaction.ReferenceNumber,
                    transaction.Description,
                    TransactionDate = transaction.TransactionDate.ToString("O"),
                    transaction.Amount,
                    CreatedBy = userId,
                    CreatedDateUtc = DateTime.UtcNow.ToString("O")
                },
                cancellationToken: cancellationToken));
    }

    public async Task<JobCostSummaryDto> GetSummaryAsync(int jobId, CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var job = await connection.QuerySingleOrDefaultAsync<Job>(
            new CommandDefinition(
                """
                SELECT j.JobId, j.JobNumber, j.EstimateId, j.JobName, j.Status, j.Version
                FROM Job j
                WHERE j.JobId = @JobId;
                """,
                new { JobId = jobId },
                cancellationToken: cancellationToken));

        if (job is null)
        {
            throw new ValidationException("Job not found.");
        }

        var rows = await connection.QueryAsync<JobCostAggregateRow>(
            new CommandDefinition(
                """
                WITH CostCodes AS (
                    SELECT CostCodeId FROM JobBudgetLine WHERE JobId = @JobId
                    UNION
                    SELECT pol.CostCodeId
                    FROM PurchaseOrderLine pol
                    INNER JOIN PurchaseOrder po ON po.PurchaseOrderId = pol.PurchaseOrderId
                    WHERE po.JobId = @JobId AND po.Status IN ('Issued','Closed')
                    UNION
                    SELECT CostCodeId FROM JobCostTransaction WHERE JobId = @JobId
                )
                SELECT cc.CostCodeId,
                       cc.Code AS CostCode,
                       cc.Description,
                       COALESCE(b.Budget, 0) AS Budget,
                       COALESCE(c.Committed, 0) AS Committed,
                       COALESCE(a.Actual, 0) AS Actual
                FROM CostCodes codes
                INNER JOIN CostCode cc ON cc.CostCodeId = codes.CostCodeId
                LEFT JOIN (
                    SELECT CostCodeId, SUM(BudgetAmount) AS Budget
                    FROM JobBudgetLine
                    WHERE JobId = @JobId
                    GROUP BY CostCodeId
                ) b ON b.CostCodeId = cc.CostCodeId
                LEFT JOIN (
                    SELECT pol.CostCodeId, SUM(pol.Amount) AS Committed
                    FROM PurchaseOrderLine pol
                    INNER JOIN PurchaseOrder po ON po.PurchaseOrderId = pol.PurchaseOrderId
                    WHERE po.JobId = @JobId AND po.Status IN ('Issued','Closed')
                    GROUP BY pol.CostCodeId
                ) c ON c.CostCodeId = cc.CostCodeId
                LEFT JOIN (
                    SELECT CostCodeId, SUM(Amount) AS Actual
                    FROM JobCostTransaction
                    WHERE JobId = @JobId
                    GROUP BY CostCodeId
                ) a ON a.CostCodeId = cc.CostCodeId
                ORDER BY cc.Code;
                """,
                new { JobId = jobId },
                cancellationToken: cancellationToken));

        var lines = rows.Select(r => new JobCostLineDto(
            r.CostCodeId,
            r.CostCode,
            r.Description,
            r.Budget,
            r.Committed,
            r.Actual,
            r.Budget - r.Committed)).ToList();

        return new JobCostSummaryDto(
            job.JobId,
            job.JobNumber,
            job.JobName,
            lines,
            lines.Sum(l => l.Budget),
            lines.Sum(l => l.Committed),
            lines.Sum(l => l.Actual),
            lines.Sum(l => l.Variance));
    }

    private sealed class JobCostTransactionRow
    {
        public int JobCostTransactionId { get; set; }
        public int JobId { get; set; }
        public int CostCodeId { get; set; }
        public string CostCode { get; set; } = string.Empty;
        public string CostCodeDescription { get; set; } = string.Empty;
        public int? VendorId { get; set; }
        public string? VendorName { get; set; }
        public int? PurchaseOrderId { get; set; }
        public string? PONumber { get; set; }
        public string? ReferenceNumber { get; set; }
        public string? Description { get; set; }
        public string TransactionDate { get; set; } = string.Empty;
        public decimal Amount { get; set; }
    }

    private sealed class JobCostAggregateRow
    {
        public int CostCodeId { get; set; }
        public string CostCode { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal Budget { get; set; }
        public decimal Committed { get; set; }
        public decimal Actual { get; set; }
    }
}
