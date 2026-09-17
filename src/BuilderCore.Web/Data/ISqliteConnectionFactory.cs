using Microsoft.Data.Sqlite;

namespace BuilderCore.Web.Data;

public interface ISqliteConnectionFactory
{
    Task<SqliteConnection> CreateOpenConnectionAsync(CancellationToken cancellationToken = default);
}
