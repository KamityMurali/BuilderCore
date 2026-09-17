using BuilderCore.Web.Models;
using Dapper;
using Microsoft.Data.Sqlite;

namespace BuilderCore.Web.Data.Repositories;

public sealed class PurchaseOrderRepository(ISqliteConnectionFactory connectionFactory)
{
    public async Task<IReadOnlyList<PurchaseOrder>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<PurchaseOrder>(
            new CommandDefinition(
                """
                SELECT po.PurchaseOrderId, po.PONumber, po.JobId, j.JobNumber, j.JobName,
                       po.VendorId, v.VendorName, po.Status, po.Version
                FROM PurchaseOrder po
                INNER JOIN Job j ON j.JobId = po.JobId
                INNER JOIN Vendor v ON v.VendorId = po.VendorId
                ORDER BY po.PONumber;
                """,
                cancellationToken: cancellationToken));
        return rows.AsList();
    }

    public async Task<IReadOnlyList<PurchaseOrder>> GetByJobIdAsync(int jobId, CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<PurchaseOrder>(
            new CommandDefinition(
                """
                SELECT po.PurchaseOrderId, po.PONumber, po.JobId, j.JobNumber, j.JobName,
                       po.VendorId, v.VendorName, po.Status, po.Version
                FROM PurchaseOrder po
                INNER JOIN Job j ON j.JobId = po.JobId
                INNER JOIN Vendor v ON v.VendorId = po.VendorId
                WHERE po.JobId = @JobId
                ORDER BY po.PONumber;
                """,
                new { JobId = jobId },
                cancellationToken: cancellationToken));
        return rows.AsList();
    }

    public async Task<PurchaseOrder?> GetByIdAsync(int purchaseOrderId, CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<PurchaseOrder>(
            new CommandDefinition(
                """
                SELECT po.PurchaseOrderId, po.PONumber, po.JobId, j.JobNumber, j.JobName,
                       po.VendorId, v.VendorName, po.Status, po.Version
                FROM PurchaseOrder po
                INNER JOIN Job j ON j.JobId = po.JobId
                INNER JOIN Vendor v ON v.VendorId = po.VendorId
                WHERE po.PurchaseOrderId = @PurchaseOrderId;
                """,
                new { PurchaseOrderId = purchaseOrderId },
                cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<PurchaseOrderLine>> GetLinesAsync(int purchaseOrderId, CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<PurchaseOrderLine>(
            new CommandDefinition(
                """
                SELECT pol.PurchaseOrderLineId, pol.PurchaseOrderId, pol.CostCodeId, cc.Code AS CostCode,
                       cc.Description AS CostCodeDescription, pol.Description, pol.Amount
                FROM PurchaseOrderLine pol
                INNER JOIN CostCode cc ON cc.CostCodeId = pol.CostCodeId
                WHERE pol.PurchaseOrderId = @PurchaseOrderId
                ORDER BY pol.PurchaseOrderLineId;
                """,
                new { PurchaseOrderId = purchaseOrderId },
                cancellationToken: cancellationToken));
        return rows.AsList();
    }

    public async Task<decimal> GetTotalAsync(int purchaseOrderId, CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await connection.ExecuteScalarAsync<decimal>(
            new CommandDefinition(
                """
                SELECT COALESCE(SUM(Amount), 0)
                FROM PurchaseOrderLine
                WHERE PurchaseOrderId = @PurchaseOrderId;
                """,
                new { PurchaseOrderId = purchaseOrderId },
                cancellationToken: cancellationToken));
    }

    public async Task<int> CreateAsync(PurchaseOrder purchaseOrder, string userId, CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(
                """
                INSERT INTO PurchaseOrder (PONumber, JobId, VendorId, Status, Version, CreatedBy, CreatedDateUtc)
                VALUES (@PONumber, @JobId, @VendorId, 'Draft', 1, @CreatedBy, @CreatedDateUtc);
                SELECT last_insert_rowid();
                """,
                new
                {
                    purchaseOrder.PONumber,
                    purchaseOrder.JobId,
                    purchaseOrder.VendorId,
                    CreatedBy = userId,
                    CreatedDateUtc = DateTime.UtcNow.ToString("O")
                },
                cancellationToken: cancellationToken));
    }

    public async Task UpdateHeaderAsync(PurchaseOrder purchaseOrder, string userId, CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var affected = await connection.ExecuteAsync(
            new CommandDefinition(
                """
                UPDATE PurchaseOrder
                SET JobId = @JobId,
                    VendorId = @VendorId,
                    UpdatedBy = @UpdatedBy,
                    UpdatedDateUtc = @UpdatedDateUtc,
                    Version = Version + 1
                WHERE PurchaseOrderId = @PurchaseOrderId AND Status = 'Draft' AND Version = @Version;
                """,
                new
                {
                    purchaseOrder.PurchaseOrderId,
                    purchaseOrder.JobId,
                    purchaseOrder.VendorId,
                    purchaseOrder.Version,
                    UpdatedBy = userId,
                    UpdatedDateUtc = DateTime.UtcNow.ToString("O")
                },
                cancellationToken: cancellationToken));

        if (affected == 0)
        {
            throw new DbUpdateConcurrencyException();
        }
    }

    public async Task ReplaceLinesAsync(int purchaseOrderId, IReadOnlyList<PurchaseOrderLine> lines, CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            await connection.ExecuteAsync(
                new CommandDefinition(
                    "DELETE FROM PurchaseOrderLine WHERE PurchaseOrderId = @PurchaseOrderId;",
                    new { PurchaseOrderId = purchaseOrderId },
                    transaction,
                    cancellationToken: cancellationToken));

            foreach (var line in lines)
            {
                await connection.ExecuteAsync(
                    new CommandDefinition(
                        """
                        INSERT INTO PurchaseOrderLine (PurchaseOrderId, CostCodeId, Description, Amount)
                        VALUES (@PurchaseOrderId, @CostCodeId, @Description, @Amount);
                        """,
                        new
                        {
                            PurchaseOrderId = purchaseOrderId,
                            line.CostCodeId,
                            line.Description,
                            line.Amount
                        },
                        transaction,
                        cancellationToken: cancellationToken));
            }

            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task IssueAsync(int purchaseOrderId, int expectedVersion, string userId, CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var affected = await connection.ExecuteAsync(
            new CommandDefinition(
                """
                UPDATE PurchaseOrder
                SET Status = 'Issued',
                    UpdatedBy = @UpdatedBy,
                    UpdatedDateUtc = @UpdatedDateUtc,
                    Version = Version + 1
                WHERE PurchaseOrderId = @PurchaseOrderId AND Status = 'Draft' AND Version = @ExpectedVersion;
                """,
                new
                {
                    PurchaseOrderId = purchaseOrderId,
                    ExpectedVersion = expectedVersion,
                    UpdatedBy = userId,
                    UpdatedDateUtc = DateTime.UtcNow.ToString("O")
                },
                cancellationToken: cancellationToken));

        if (affected == 0)
        {
            throw new DbUpdateConcurrencyException();
        }
    }

    public async Task CloseAsync(int purchaseOrderId, int expectedVersion, string userId, CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var affected = await connection.ExecuteAsync(
            new CommandDefinition(
                """
                UPDATE PurchaseOrder
                SET Status = 'Closed',
                    UpdatedBy = @UpdatedBy,
                    UpdatedDateUtc = @UpdatedDateUtc,
                    Version = Version + 1
                WHERE PurchaseOrderId = @PurchaseOrderId AND Status = 'Issued' AND Version = @ExpectedVersion;
                """,
                new
                {
                    PurchaseOrderId = purchaseOrderId,
                    ExpectedVersion = expectedVersion,
                    UpdatedBy = userId,
                    UpdatedDateUtc = DateTime.UtcNow.ToString("O")
                },
                cancellationToken: cancellationToken));

        if (affected == 0)
        {
            throw new DbUpdateConcurrencyException();
        }
    }

    public async Task<bool> NumberExistsAsync(string poNumber, int? excludeId, CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var count = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(
                """
                SELECT COUNT(1) FROM PurchaseOrder
                WHERE PONumber = @PONumber AND (@ExcludeId IS NULL OR PurchaseOrderId <> @ExcludeId);
                """,
                new { PONumber = poNumber, ExcludeId = excludeId },
                cancellationToken: cancellationToken));
        return count > 0;
    }

    public async Task<int> CountOpenAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(
                "SELECT COUNT(1) FROM PurchaseOrder WHERE Status IN ('Draft','Issued');",
                cancellationToken: cancellationToken));
    }
}
