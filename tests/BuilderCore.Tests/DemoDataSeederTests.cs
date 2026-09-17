using BuilderCore.Web.Data;
using BuilderCore.Web.Data.Repositories;
using BuilderCore.Web.Hubs;
using BuilderCore.Web.Services;
using BuilderCore.Tests.TestFixtures;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using NSubstitute;

namespace BuilderCore.Tests;

public class DemoDataSeederTests
{
    [Fact]
    public async Task Seeder_IsIdempotent()
    {
        await using var fixture = await CreateDemoFixtureAsync();
        var seeder = fixture.GetRequiredService<DemoDataSeeder>();

        await seeder.SeedAsync();
        await seeder.SeedAsync();

        var estimateService = fixture.GetRequiredService<IEstimateService>();
        var estimates = await estimateService.GetAllAsync(null);
        Assert.Single(estimates, e => e.EstimateNumber == "EST-1001");
    }

    [Fact]
    public async Task DemoJob1001_HasExpectedTotals()
    {
        await using var fixture = await CreateDemoFixtureAsync();
        await fixture.GetRequiredService<DemoDataSeeder>().SeedAsync();

        var jobService = fixture.GetRequiredService<IJobService>();
        var jobCostService = fixture.GetRequiredService<IJobCostService>();
        var jobs = await jobService.GetAllAsync();
        var job = jobs.Single(j => j.JobNumber == "JOB-1001");
        var summary = await jobCostService.GetSummaryAsync(job.JobId);

        Assert.Equal(290000m, summary.Budget);
        Assert.Equal(169800m, summary.Committed);
        Assert.Equal(104250m, summary.Actual);
        Assert.Equal(120200m, summary.Variance);
    }

    [Fact]
    public async Task DemoJob1002_HasNegativeCostCodeVariance()
    {
        await using var fixture = await CreateDemoFixtureAsync();
        await fixture.GetRequiredService<DemoDataSeeder>().SeedAsync();

        var jobService = fixture.GetRequiredService<IJobService>();
        var jobCostService = fixture.GetRequiredService<IJobCostService>();
        var job = (await jobService.GetAllAsync()).Single(j => j.JobNumber == "JOB-1002");
        var summary = await jobCostService.GetSummaryAsync(job.JobId);

        Assert.Contains(summary.Lines, l => l.CostCode == "3000" && l.Variance < 0);
    }

    [Fact]
    public async Task Est1003_RemainsDraftWithoutJob()
    {
        await using var fixture = await CreateDemoFixtureAsync();
        await fixture.GetRequiredService<DemoDataSeeder>().SeedAsync();

        var estimateService = fixture.GetRequiredService<IEstimateService>();
        var estimate = (await estimateService.GetAllAsync(null)).Single(e => e.EstimateNumber == "EST-1003");
        Assert.Equal("Draft", estimate.Status);
        Assert.Null(estimate.JobId);
    }

    [Fact]
    public async Task Job1004_IsClosed()
    {
        await using var fixture = await CreateDemoFixtureAsync();
        await fixture.GetRequiredService<DemoDataSeeder>().SeedAsync();

        var jobService = fixture.GetRequiredService<IJobService>();
        var job = (await jobService.GetAllAsync()).Single(j => j.JobNumber == "JOB-1004");
        Assert.Equal("Closed", job.Status);
    }

    private static async Task<DemoFixture> CreateDemoFixtureAsync()
    {
        var fixture = new DemoFixture();
        await fixture.InitializeAsync();
        return fixture;
    }

    private sealed class DemoFixture : IAsyncDisposable
    {
        private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"buildercore-demo-{Guid.NewGuid():N}.db");
        private ServiceProvider? _serviceProvider;

        public T GetRequiredService<T>() where T : notnull => _serviceProvider!.GetRequiredService<T>();

        public async Task InitializeAsync()
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:DefaultConnection"] = $"Data Source={_databasePath};Foreign Keys=True"
                })
                .Build();

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<IConfiguration>(configuration);
            services.AddSingleton<ISqliteConnectionFactory, SqliteConnectionFactory>();
            services.AddSingleton<IHostEnvironment>(new DemoHostEnvironment());
            services.AddSingleton<DatabaseInitializer>();
            services.AddSingleton<DemoDataSeeder>();
            services.AddSingleton<CostCodeRepository>();
            services.AddSingleton<EstimateRepository>();
            services.AddSingleton<JobRepository>();
            services.AddSingleton<PurchaseOrderRepository>();
            services.AddSingleton<JobCostRepository>();
            services.AddSingleton<VendorRepository>();
            services.AddSingleton<ICurrentUserService>(new TestCurrentUserService("demo-seed-user"));
            services.AddSingleton<IHubContext<JobCostHub>>(Substitute.For<IHubContext<JobCostHub>>());
            services.AddSingleton<IEstimateService, EstimateService>();
            services.AddSingleton<IJobService, JobService>();
            services.AddSingleton<IPurchaseOrderService, PurchaseOrderService>();
            services.AddSingleton<IJobCostService, JobCostService>();

            _serviceProvider = services.BuildServiceProvider();
            await _serviceProvider.GetRequiredService<DatabaseInitializer>().InitializeAsync();
        }

        public async ValueTask DisposeAsync()
        {
            if (_serviceProvider is not null)
            {
                await _serviceProvider.DisposeAsync();
            }
        }

        private sealed class DemoHostEnvironment : IHostEnvironment
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

                throw new DirectoryNotFoundException("Could not locate repository root.");
            }
        }
    }
}
