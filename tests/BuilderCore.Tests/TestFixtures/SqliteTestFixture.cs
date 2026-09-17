using BuilderCore.Web.Data;
using BuilderCore.Web.Data.Repositories;
using BuilderCore.Web.Hubs;
using BuilderCore.Web.Services;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace BuilderCore.Tests.TestFixtures;

public sealed class SqliteTestFixture : IAsyncLifetime
{
    private readonly string _databasePath;
    private ServiceProvider? _serviceProvider;

    public SqliteTestFixture()
    {
        _databasePath = Path.Combine(Path.GetTempPath(), $"buildercore-test-{Guid.NewGuid():N}.db");
    }

    public IServiceProvider Services => _serviceProvider
        ?? throw new InvalidOperationException("Fixture not initialized.");

    public string TestUserId { get; } = "test-user";

    public async Task InitializeAsync()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = $"Data Source={_databasePath};Foreign Keys=True",
                ["DemoData:Enabled"] = "false"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddSingleton<ISqliteConnectionFactory, SqliteConnectionFactory>();
        services.AddSingleton<IHostEnvironment>(new TestHostEnvironment());
        services.AddSingleton<DatabaseInitializer>();
        services.AddSingleton<DemoDataSeeder>();
        services.AddSingleton<CostCodeRepository>();
        services.AddSingleton<EstimateRepository>();
        services.AddSingleton<JobRepository>();
        services.AddSingleton<PurchaseOrderRepository>();
        services.AddSingleton<JobCostRepository>();
        services.AddSingleton<VendorRepository>();
        services.AddSingleton<ICurrentUserService>(new TestCurrentUserService(TestUserId));
        services.AddSingleton<IHubContext<JobCostHub>>(Substitute.For<IHubContext<JobCostHub>>());
        services.AddSingleton<IEstimateService, EstimateService>();
        services.AddSingleton<IJobService, JobService>();
        services.AddSingleton<IPurchaseOrderService, PurchaseOrderService>();
        services.AddSingleton<IJobCostService, JobCostService>();
        services.AddSingleton<ICostCodeService, CostCodeService>();
        services.AddSingleton<IVendorService, VendorService>();

        _serviceProvider = services.BuildServiceProvider();

        var initializer = _serviceProvider.GetRequiredService<DatabaseInitializer>();
        await initializer.InitializeAsync();
    }

    public T GetRequiredService<T>() where T : notnull => Services.GetRequiredService<T>();

    public async Task DisposeAsync()
    {
        if (_serviceProvider is not null)
        {
            await _serviceProvider.DisposeAsync();
        }

        TryDelete(_databasePath);
        TryDelete(_databasePath + "-wal");
        TryDelete(_databasePath + "-shm");
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // Best effort cleanup for temp test files.
        }
    }

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "BuilderCore.Tests";
        public string ContentRootPath { get; set; } = FindContentRoot();
        public IFileProvider ContentRootFileProvider { get; set; } = null!;

        private static string FindContentRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir is not null)
            {
                if (Directory.Exists(Path.Combine(dir.FullName, "database")))
                {
                    return dir.FullName;
                }
                dir = dir.Parent;
            }

            throw new DirectoryNotFoundException("Could not locate repository root for database scripts.");
        }
    }
}

public sealed class TestCurrentUserService(string userId, string? email = "test@example.com", string? name = "Test User") : ICurrentUserService
{
    public bool IsAuthenticated => true;
    public string UserId { get; } = userId;
    public string? Email { get; } = email;
    public string DisplayName { get; } = name ?? "Test User";
}
