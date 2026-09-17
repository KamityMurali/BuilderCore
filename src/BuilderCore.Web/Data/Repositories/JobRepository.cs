using BuilderCore.Web.Models;
using Dapper;
using Microsoft.Data.Sqlite;

namespace BuilderCore.Web.Data.Repositories;

public sealed class JobRepository(ISqliteConnectionFactory connectionFactory)
{
    public async Task<IReadOnlyList<Job>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<Job>(
            new CommandDefinition(
                """
                SELECT j.JobId, j.JobNumber, j.EstimateId, e.EstimateNumber, j.JobName, j.Status, j.Version
                FROM Job j
                INNER JOIN Estimate e ON e.EstimateId = j.EstimateId
                ORDER BY j.JobNumber;
                """,
                cancellationToken: cancellationToken));
        return rows.AsList();
    }

    public async Task<Job?> GetByIdAsync(int jobId, CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<Job>(
            new CommandDefinition(
                """
                SELECT j.JobId, j.JobNumber, j.EstimateId, e.EstimateNumber, j.JobName, j.Status, j.Version
                FROM Job j
                INNER JOIN Estimate e ON e.EstimateId = j.EstimateId
                WHERE j.JobId = @JobId;
                """,
                new { JobId = jobId },
                cancellationToken: cancellationToken));
    }

    public async Task<Job?> GetByEstimateIdAsync(int estimateId, CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<Job>(
            new CommandDefinition(
                """
                SELECT j.JobId, j.JobNumber, j.EstimateId, e.EstimateNumber, j.JobName, j.Status, j.Version
                FROM Job j
                INNER JOIN Estimate e ON e.EstimateId = j.EstimateId
                WHERE j.EstimateId = @EstimateId;
                """,
                new { EstimateId = estimateId },
                cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<JobBudgetLine>> GetBudgetLinesAsync(int jobId, CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<JobBudgetLine>(
            new CommandDefinition(
                """
                SELECT jbl.JobBudgetLineId, jbl.JobId, jbl.CostCodeId, cc.Code AS CostCode,
                       cc.Description AS CostCodeDescription, jbl.Description, jbl.BudgetAmount
                FROM JobBudgetLine jbl
                INNER JOIN CostCode cc ON cc.CostCodeId = jbl.CostCodeId
                WHERE jbl.JobId = @JobId
                ORDER BY cc.Code;
                """,
                new { JobId = jobId },
                cancellationToken: cancellationToken));
        return rows.AsList();
    }

    public async Task<int> CreateFromEstimateAsync(
        string jobNumber,
        string jobName,
        int estimateId,
        IReadOnlyList<EstimateLine> estimateLines,
        string userId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            var jobId = await connection.ExecuteScalarAsync<int>(
                new CommandDefinition(
                    """
                    INSERT INTO Job (JobNumber, EstimateId, JobName, Status, Version, CreatedBy, CreatedDateUtc)
                    VALUES (@JobNumber, @EstimateId, @JobName, 'Active', 1, @CreatedBy, @CreatedDateUtc);
                    SELECT last_insert_rowid();
                    """,
                    new
                    {
                        JobNumber = jobNumber,
                        EstimateId = estimateId,
                        JobName = jobName,
                        CreatedBy = userId,
                        CreatedDateUtc = DateTime.UtcNow.ToString("O")
                    },
                    transaction,
                    cancellationToken: cancellationToken));

            foreach (var line in estimateLines)
            {
                await connection.ExecuteAsync(
                    new CommandDefinition(
                        """
                        INSERT INTO JobBudgetLine (JobId, CostCodeId, Description, BudgetAmount)
                        VALUES (@JobId, @CostCodeId, @Description, @BudgetAmount);
                        """,
                        new
                        {
                            JobId = jobId,
                            line.CostCodeId,
                            line.Description,
                            BudgetAmount = line.Quantity * line.UnitCost
                        },
                        transaction,
                        cancellationToken: cancellationToken));
            }

            await transaction.CommitAsync(cancellationToken);
            return jobId;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task CloseAsync(int jobId, int expectedVersion, string userId, CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var affected = await connection.ExecuteAsync(
            new CommandDefinition(
                """
                UPDATE Job
                SET Status = 'Closed',
                    UpdatedBy = @UpdatedBy,
                    UpdatedDateUtc = @UpdatedDateUtc,
                    Version = Version + 1
                WHERE JobId = @JobId AND Status = 'Active' AND Version = @ExpectedVersion;
                """,
                new
                {
                    JobId = jobId,
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

    public async Task<bool> NumberExistsAsync(string jobNumber, CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var count = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(
                "SELECT COUNT(1) FROM Job WHERE JobNumber = @JobNumber;",
                new { JobNumber = jobNumber },
                cancellationToken: cancellationToken));
        return count > 0;
    }

    public async Task<int> CountByStatusAsync(string status, CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(
                "SELECT COUNT(1) FROM Job WHERE Status = @Status;",
                new { Status = status },
                cancellationToken: cancellationToken));
    }
}
