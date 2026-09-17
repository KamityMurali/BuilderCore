using BuilderCore.Web.Models;
using BuilderCore.Web.Services;
using BuilderCore.Tests.TestFixtures;

namespace BuilderCore.Tests;

public class JobCostServiceTests : IClassFixture<SqliteTestFixture>
{
    private readonly SqliteTestFixture _fixture;

    public JobCostServiceTests(SqliteTestFixture fixture) => _fixture = fixture;

    private async Task<(int jobId, int foundationId, int framingId, int roofingId)> CreateJobWithBudgetOnlyAsync()
    {
        var costCodes = _fixture.GetRequiredService<ICostCodeService>();
        var foundation = (await costCodes.GetAllAsync()).First(c => c.Code == "2000");
        var framing = (await costCodes.GetAllAsync()).First(c => c.Code == "3000");
        var roofing = (await costCodes.GetAllAsync()).First(c => c.Code == "4000");
        var estimateService = _fixture.GetRequiredService<IEstimateService>();
        var jobService = _fixture.GetRequiredService<IJobService>();

        var estimateId = await estimateService.SaveAsync(new Estimate
        {
            EstimateNumber = $"EST-JC-{Guid.NewGuid():N}",
            ProjectName = "Job Cost Project"
        },
        [
            new EstimateLine { CostCodeId = foundation.CostCodeId, Quantity = 1, UnitCost = 35000m },
            new EstimateLine { CostCodeId = framing.CostCodeId, Quantity = 1, UnitCost = 72000m }
        ]);
        var estimate = await estimateService.GetByIdAsync(estimateId);
        await estimateService.ApproveAsync(estimateId, estimate!.Version);
        var jobId = await jobService.CreateFromEstimateAsync(estimateId, $"JOB-JC-{Guid.NewGuid():N}", "Job Cost Project");
        return (jobId, foundation.CostCodeId, framing.CostCodeId, roofing.CostCodeId);
    }

    [Fact]
    public async Task JobCost_IncludesBudgetOnlyCommittedOnlyActualOnlyAndCombined()
    {
        var (jobId, foundationId, framingId, roofingId) = await CreateJobWithBudgetOnlyAsync();
        var poService = _fixture.GetRequiredService<IPurchaseOrderService>();
        var jobCostService = _fixture.GetRequiredService<IJobCostService>();
        var vendors = _fixture.GetRequiredService<IVendorService>();
        var vendor = (await vendors.GetAllAsync()).First();

        var poId = await poService.SaveAsync(new PurchaseOrder
        {
            PONumber = $"PO-JC-{Guid.NewGuid():N}",
            JobId = jobId,
            VendorId = vendor.VendorId
        },
        [
            new PurchaseOrderLine { CostCodeId = framingId, Amount = 68000m }
        ]);
        var po = await poService.GetByIdAsync(poId);
        await poService.IssueAsync(poId, po!.Version);

        await jobCostService.PostActualCostAsync(new JobCostTransaction
        {
            JobId = jobId,
            CostCodeId = roofingId,
            TransactionDate = DateOnly.FromDateTime(DateTime.UtcNow),
            Amount = 5000m
        });

        var summary = await jobCostService.GetSummaryAsync(jobId);

        var foundation = summary.Lines.Single(l => l.CostCodeId == foundationId);
        Assert.Equal(35000m, foundation.Budget);
        Assert.Equal(0m, foundation.Committed);
        Assert.Equal(0m, foundation.Actual);
        Assert.Equal(35000m, foundation.Variance);

        var framing = summary.Lines.Single(l => l.CostCodeId == framingId);
        Assert.Equal(72000m, framing.Budget);
        Assert.Equal(68000m, framing.Committed);
        Assert.Equal(0m, framing.Actual);
        Assert.Equal(4000m, framing.Variance);

        var roofing = summary.Lines.Single(l => l.CostCodeId == roofingId);
        Assert.Equal(0m, roofing.Budget);
        Assert.Equal(0m, roofing.Committed);
        Assert.Equal(5000m, roofing.Actual);
        Assert.Equal(0m, roofing.Variance);

        Assert.Equal(summary.Lines.Sum(l => l.Budget), summary.Budget);
        Assert.Equal(summary.Lines.Sum(l => l.Committed), summary.Committed);
        Assert.Equal(summary.Lines.Sum(l => l.Actual), summary.Actual);
        Assert.Equal(summary.Lines.Sum(l => l.Variance), summary.Variance);
    }
}
