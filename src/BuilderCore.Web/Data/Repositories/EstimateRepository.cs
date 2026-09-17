using BuilderCore.Web.Models;
using Dapper;
using Microsoft.Data.Sqlite;

namespace BuilderCore.Web.Data.Repositories;

public sealed class EstimateRepository(ISqliteConnectionFactory connectionFactory)
{
    public async Task<IReadOnlyList<Estimate>> GetAllAsync(string? search, CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<Estimate>(
            new CommandDefinition(
                """
                SELECT e.EstimateId, e.EstimateNumber, e.ProjectName, e.Description, e.Status, e.Version,
                       j.JobId
                FROM Estimate e
                LEFT JOIN Job j ON j.EstimateId = e.EstimateId
                WHERE (@Search IS NULL OR @Search = ''
                       OR e.EstimateNumber LIKE '%' || @Search || '%'
                       OR e.ProjectName LIKE '%' || @Search || '%')
                ORDER BY e.EstimateNumber;
                """,
                new { Search = search },
                cancellationToken: cancellationToken));
        return rows.AsList();
    }

    public async Task<Estimate?> GetByIdAsync(int estimateId, CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<Estimate>(
            new CommandDefinition(
                """
                SELECT e.EstimateId, e.EstimateNumber, e.ProjectName, e.Description, e.Status, e.Version,
                       j.JobId
                FROM Estimate e
                LEFT JOIN Job j ON j.EstimateId = e.EstimateId
                WHERE e.EstimateId = @EstimateId;
                """,
                new { EstimateId = estimateId },
                cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<EstimateLine>> GetLinesAsync(int estimateId, CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<EstimateLine>(
            new CommandDefinition(
                """
                SELECT el.EstimateLineId, el.EstimateId, el.CostCodeId, cc.Code AS CostCode,
                       cc.Description AS CostCodeDescription, el.Description, el.Quantity, el.UnitCost
                FROM EstimateLine el
                INNER JOIN CostCode cc ON cc.CostCodeId = el.CostCodeId
                WHERE el.EstimateId = @EstimateId
                ORDER BY el.EstimateLineId;
                """,
                new { EstimateId = estimateId },
                cancellationToken: cancellationToken));
        return rows.AsList();
    }

    public async Task<decimal> GetTotalAsync(int estimateId, CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await connection.ExecuteScalarAsync<decimal>(
            new CommandDefinition(
                """
                SELECT COALESCE(SUM(Quantity * UnitCost), 0)
                FROM EstimateLine
                WHERE EstimateId = @EstimateId;
                """,
                new { EstimateId = estimateId },
                cancellationToken: cancellationToken));
    }

    public async Task<int> CreateAsync(Estimate estimate, string userId, CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(
                """
                INSERT INTO Estimate (EstimateNumber, ProjectName, Description, Status, Version, CreatedBy, CreatedDateUtc)
                VALUES (@EstimateNumber, @ProjectName, @Description, @Status, 1, @CreatedBy, @CreatedDateUtc);
                SELECT last_insert_rowid();
                """,
                new
                {
                    estimate.EstimateNumber,
                    estimate.ProjectName,
                    estimate.Description,
                    estimate.Status,
                    CreatedBy = userId,
                    CreatedDateUtc = DateTime.UtcNow.ToString("O")
                },
                cancellationToken: cancellationToken));
    }

    public async Task UpdateHeaderAsync(Estimate estimate, string userId, CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var affected = await connection.ExecuteAsync(
            new CommandDefinition(
                """
                UPDATE Estimate
                SET ProjectName = @ProjectName,
                    Description = @Description,
                    UpdatedBy = @UpdatedBy,
                    UpdatedDateUtc = @UpdatedDateUtc,
                    Version = Version + 1
                WHERE EstimateId = @EstimateId AND Status = 'Draft' AND Version = @Version;
                """,
                new
                {
                    estimate.EstimateId,
                    estimate.ProjectName,
                    estimate.Description,
                    estimate.Version,
                    UpdatedBy = userId,
                    UpdatedDateUtc = DateTime.UtcNow.ToString("O")
                },
                cancellationToken: cancellationToken));

        if (affected == 0)
        {
            throw new DbUpdateConcurrencyException();
        }
    }

    public async Task ReplaceLinesAsync(int estimateId, IReadOnlyList<EstimateLine> lines, CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            await connection.ExecuteAsync(
                new CommandDefinition(
                    "DELETE FROM EstimateLine WHERE EstimateId = @EstimateId;",
                    new { EstimateId = estimateId },
                    transaction,
                    cancellationToken: cancellationToken));

            foreach (var line in lines)
            {
                await connection.ExecuteAsync(
                    new CommandDefinition(
                        """
                        INSERT INTO EstimateLine (EstimateId, CostCodeId, Description, Quantity, UnitCost)
                        VALUES (@EstimateId, @CostCodeId, @Description, @Quantity, @UnitCost);
                        """,
                        new
                        {
                            EstimateId = estimateId,
                            line.CostCodeId,
                            line.Description,
                            line.Quantity,
                            line.UnitCost
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

    public async Task ApproveAsync(int estimateId, int expectedVersion, string userId, CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var affected = await connection.ExecuteAsync(
            new CommandDefinition(
                """
                UPDATE Estimate
                SET Status = 'Approved',
                    UpdatedBy = @UpdatedBy,
                    UpdatedDateUtc = @UpdatedDateUtc,
                    Version = Version + 1
                WHERE EstimateId = @EstimateId AND Status = 'Draft' AND Version = @ExpectedVersion;
                """,
                new
                {
                    EstimateId = estimateId,
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

    public async Task<bool> NumberExistsAsync(string estimateNumber, int? excludeId, CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var count = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(
                """
                SELECT COUNT(1) FROM Estimate
                WHERE EstimateNumber = @EstimateNumber AND (@ExcludeId IS NULL OR EstimateId <> @ExcludeId);
                """,
                new { EstimateNumber = estimateNumber, ExcludeId = excludeId },
                cancellationToken: cancellationToken));
        return count > 0;
    }

    public async Task<int> CountByStatusAsync(string status, CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(
                "SELECT COUNT(1) FROM Estimate WHERE Status = @Status;",
                new { Status = status },
                cancellationToken: cancellationToken));
    }
}
