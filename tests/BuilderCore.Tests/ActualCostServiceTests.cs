using BuilderCore.Web.Models;
using BuilderCore.Web.Services;
using BuilderCore.Tests.TestFixtures;

namespace BuilderCore.Tests;

public class ActualCostServiceTests : IClassFixture<SqliteTestFixture>
{
    private readonly SqliteTestFixture _fixture;

    public ActualCostServiceTests(SqliteTestFixture fixture) => _fixture = fixture;

    private async Task<(int jobId, int otherJobId, int costCodeId, int poOnJobId)> SetupJobsAsync()
    {
        var costCodes = _fixture.GetRequiredService<ICostCodeService>();
        var cc = (await costCodes.GetAllAsync()).First(c => c.Code == "3000");
        var vendors = _fixture.GetRequiredService<IVendorService>();
        var vendor = (await vendors.GetAllAsync()).First();
        var estimateService = _fixture.GetRequiredService<IEstimateService>();
        var jobService = _fixture.GetRequiredService<IJobService>();
        var poService = _fixture.GetRequiredService<IPurchaseOrderService>();

        async Task<int> CreateJob(string suffix)
        {
            var estimateId = await estimateService.SaveAsync(new Estimate
            {
                EstimateNumber = $"EST-AC-{suffix}",
                ProjectName = $"Project {suffix}"
            },
            [
                new EstimateLine { CostCodeId = cc.CostCodeId, Quantity = 1, UnitCost = 1000m }
            ]);
            var estimate = await estimateService.GetByIdAsync(estimateId);
            await estimateService.ApproveAsync(estimateId, estimate!.Version);
            return await jobService.CreateFromEstimateAsync(estimateId, $"JOB-AC-{suffix}", $"Project {suffix}");
        }

        var jobId = await CreateJob(Guid.NewGuid().ToString("N"));
        var otherJobId = await CreateJob(Guid.NewGuid().ToString("N"));

        var poId = await poService.SaveAsync(new PurchaseOrder
        {
            PONumber = $"PO-OTHER-{Guid.NewGuid():N}",
            JobId = otherJobId,
            VendorId = vendor.VendorId
        },
        [
            new PurchaseOrderLine { CostCodeId = cc.CostCodeId, Amount = 500m }
        ]);
        var po = await poService.GetByIdAsync(poId);
        await poService.IssueAsync(poId, po!.Version);

        return (jobId, otherJobId, cc.CostCodeId, poId);
    }

    [Fact]
    public async Task PositiveCostPostsSuccessfully()
    {
        var (jobId, _, costCodeId, _) = await SetupJobsAsync();
        var service = _fixture.GetRequiredService<IJobCostService>();

        await service.PostActualCostAsync(new JobCostTransaction
        {
            JobId = jobId,
            CostCodeId = costCodeId,
            TransactionDate = DateOnly.FromDateTime(DateTime.UtcNow),
            Amount = 100m
        });

        var summary = await service.GetSummaryAsync(jobId);
        Assert.Equal(100m, summary.Actual);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    public async Task ZeroOrNegativeAmountRejected(decimal amount)
    {
        var (jobId, _, costCodeId, _) = await SetupJobsAsync();
        var service = _fixture.GetRequiredService<IJobCostService>();

        var ex = await Assert.ThrowsAsync<ValidationException>(() => service.PostActualCostAsync(new JobCostTransaction
        {
            JobId = jobId,
            CostCodeId = costCodeId,
            TransactionDate = DateOnly.FromDateTime(DateTime.UtcNow),
            Amount = amount
        }));
        Assert.Contains("greater than zero", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PoFromAnotherJobRejected()
    {
        var (jobId, _, costCodeId, otherPoId) = await SetupJobsAsync();
        var service = _fixture.GetRequiredService<IJobCostService>();

        var ex = await Assert.ThrowsAsync<ValidationException>(() => service.PostActualCostAsync(new JobCostTransaction
        {
            JobId = jobId,
            CostCodeId = costCodeId,
            PurchaseOrderId = otherPoId,
            TransactionDate = DateOnly.FromDateTime(DateTime.UtcNow),
            Amount = 100m
        }));
        Assert.Contains("same job", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ClosedJobRejected()
    {
        var (jobId, _, costCodeId, _) = await SetupJobsAsync();
        var jobService = _fixture.GetRequiredService<IJobService>();
        var service = _fixture.GetRequiredService<IJobCostService>();

        var job = await jobService.GetByIdAsync(jobId);
        await jobService.CloseAsync(jobId, job!.Version);

        var ex = await Assert.ThrowsAsync<ValidationException>(() => service.PostActualCostAsync(new JobCostTransaction
        {
            JobId = jobId,
            CostCodeId = costCodeId,
            TransactionDate = DateOnly.FromDateTime(DateTime.UtcNow),
            Amount = 100m
        }));
        Assert.Contains("active", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}
