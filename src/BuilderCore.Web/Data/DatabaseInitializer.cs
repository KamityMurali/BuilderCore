using Dapper;
using Microsoft.Data.Sqlite;

namespace BuilderCore.Web.Data;

public sealed class DatabaseInitializer(
    ISqliteConnectionFactory connectionFactory,
    ILogger<DatabaseInitializer> logger)
{
    private static readonly string[] ScriptOrder =
    [
        "001_schema.sql",
        "002_seed.sql"
    ];

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await EnsureSchemaVersionTableAsync(connection, cancellationToken);

        var databasePath = ResolveDatabaseDirectory();
        foreach (var scriptName in ScriptOrder)
        {
            var scriptPath = Path.Combine(databasePath, scriptName);
            if (!File.Exists(scriptPath))
            {
                throw new FileNotFoundException($"Database script not found: {scriptPath}");
            }

            var version = Path.GetFileNameWithoutExtension(scriptName);
            if (await IsScriptAppliedAsync(connection, version, cancellationToken))
            {
                continue;
            }

            logger.LogInformation("Applying database script {ScriptName}", scriptName);
            var sql = await File.ReadAllTextAsync(scriptPath, cancellationToken);
            await connection.ExecuteAsync(new CommandDefinition(sql, cancellationToken: cancellationToken));
            await connection.ExecuteAsync(
                new CommandDefinition(
                    "INSERT INTO SchemaVersion (Version, AppliedDateUtc) VALUES (@Version, @AppliedDateUtc);",
                    new { Version = version, AppliedDateUtc = DateTime.UtcNow.ToString("O") },
                    cancellationToken: cancellationToken));
        }
    }

    private static string ResolveDatabaseDirectory()
    {
        var candidates = new[]
        {
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "database")),
            Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "database")),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "database"))
        };

        foreach (var candidate in candidates)
        {
            if (Directory.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new DirectoryNotFoundException("Could not locate database scripts directory.");
    }

    private static async Task EnsureSchemaVersionTableAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        const string sql = """
            CREATE TABLE IF NOT EXISTS SchemaVersion (
              Version TEXT NOT NULL PRIMARY KEY,
              AppliedDateUtc TEXT NOT NULL
            );
            """;

        await connection.ExecuteAsync(new CommandDefinition(sql, cancellationToken: cancellationToken));
    }

    private static async Task<bool> IsScriptAppliedAsync(SqliteConnection connection, string version, CancellationToken cancellationToken)
    {
        var count = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(
                "SELECT COUNT(1) FROM SchemaVersion WHERE Version = @Version;",
                new { Version = version },
                cancellationToken: cancellationToken));
        return count > 0;
    }
}
