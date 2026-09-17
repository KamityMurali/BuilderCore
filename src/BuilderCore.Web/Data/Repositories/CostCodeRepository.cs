using BuilderCore.Web.Models;
using Dapper;
using Microsoft.Data.Sqlite;

namespace BuilderCore.Web.Data.Repositories;

public sealed class CostCodeRepository(ISqliteConnectionFactory connectionFactory)
{
    public async Task<IReadOnlyList<CostCode>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<CostCode>(
            new CommandDefinition(
                """
                SELECT CostCodeId, Code, Description, IsActive
                FROM CostCode
                ORDER BY Code;
                """,
                cancellationToken: cancellationToken));
        return rows.AsList();
    }

    public async Task<IReadOnlyList<CostCode>> GetActiveAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<CostCode>(
            new CommandDefinition(
                """
                SELECT CostCodeId, Code, Description, IsActive
                FROM CostCode
                WHERE IsActive = 1
                ORDER BY Code;
                """,
                cancellationToken: cancellationToken));
        return rows.AsList();
    }

    public async Task<CostCode?> GetByIdAsync(int costCodeId, CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<CostCode>(
            new CommandDefinition(
                """
                SELECT CostCodeId, Code, Description, IsActive
                FROM CostCode
                WHERE CostCodeId = @CostCodeId;
                """,
                new { CostCodeId = costCodeId },
                cancellationToken: cancellationToken));
    }

    public async Task<int> CreateAsync(CostCode costCode, string userId, CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(
                """
                INSERT INTO CostCode (Code, Description, IsActive, CreatedBy, CreatedDateUtc)
                VALUES (@Code, @Description, @IsActive, @CreatedBy, @CreatedDateUtc);
                SELECT last_insert_rowid();
                """,
                new
                {
                    costCode.Code,
                    costCode.Description,
                    IsActive = costCode.IsActive ? 1 : 0,
                    CreatedBy = userId,
                    CreatedDateUtc = DateTime.UtcNow.ToString("O")
                },
                cancellationToken: cancellationToken));
    }

    public async Task UpdateAsync(CostCode costCode, string userId, CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(
            new CommandDefinition(
                """
                UPDATE CostCode
                SET Code = @Code,
                    Description = @Description,
                    IsActive = @IsActive,
                    UpdatedBy = @UpdatedBy,
                    UpdatedDateUtc = @UpdatedDateUtc
                WHERE CostCodeId = @CostCodeId;
                """,
                new
                {
                    costCode.CostCodeId,
                    costCode.Code,
                    costCode.Description,
                    IsActive = costCode.IsActive ? 1 : 0,
                    UpdatedBy = userId,
                    UpdatedDateUtc = DateTime.UtcNow.ToString("O")
                },
                cancellationToken: cancellationToken));
    }

    public async Task<bool> CodeExistsAsync(string code, int? excludeId, CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var count = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(
                """
                SELECT COUNT(1) FROM CostCode
                WHERE Code = @Code AND (@ExcludeId IS NULL OR CostCodeId <> @ExcludeId);
                """,
                new { Code = code, ExcludeId = excludeId },
                cancellationToken: cancellationToken));
        return count > 0;
    }

    public async Task<int?> GetIdByCodeAsync(string code, SqliteConnection connection, SqliteTransaction? transaction, CancellationToken cancellationToken)
    {
        return await connection.QuerySingleOrDefaultAsync<int?>(
            new CommandDefinition(
                "SELECT CostCodeId FROM CostCode WHERE Code = @Code;",
                new { Code = code },
                transaction,
                cancellationToken: cancellationToken));
    }
}
