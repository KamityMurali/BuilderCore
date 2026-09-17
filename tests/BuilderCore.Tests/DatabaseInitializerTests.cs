using BuilderCore.Web.Data;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace BuilderCore.Tests;

public class DatabaseInitializerTests
{
    [Fact]
    public async Task Initializer_CreatesSchema_AndCanRunRepeatedly()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"buildercore-init-{Guid.NewGuid():N}.db");
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = $"Data Source={databasePath};Foreign Keys=True"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddSingleton<ISqliteConnectionFactory, SqliteConnectionFactory>();
        services.AddSingleton<DatabaseInitializer>();
        await using var provider = services.BuildServiceProvider();

        var initializer = provider.GetRequiredService<DatabaseInitializer>();
        await initializer.InitializeAsync();
        await initializer.InitializeAsync();

        Assert.True(File.Exists(databasePath));

        try { File.Delete(databasePath); } catch { /* ignore */ }
    }
}
