using Microsoft.Data.Sqlite;

namespace BuilderCore.Web.Data;

public sealed class SqliteConnectionFactory(IConfiguration configuration) : ISqliteConnectionFactory
{
    private readonly string _connectionString = configuration.GetConnectionString("DefaultConnection")
        ?? "Data Source=buildercore.db;Foreign Keys=True";

    public async Task<SqliteConnection> CreateOpenConnectionAsync(CancellationToken cancellationToken = default)
    {
        var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await connection.ExecutePragmaAsync("PRAGMA foreign_keys = ON;", cancellationToken);
        return connection;
    }
}

internal static class SqliteConnectionExtensions
{
    public static async Task ExecutePragmaAsync(this SqliteConnection connection, string sql, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
