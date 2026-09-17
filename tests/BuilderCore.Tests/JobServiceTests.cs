using BuilderCore.Web.Models;
using BuilderCore.Web.Services;
using BuilderCore.Tests.TestFixtures;

namespace BuilderCore.Tests;

public class JobServiceTests : IClassFixture<SqliteTestFixture>
{
    private readonly SqliteTestFixture _fixture;

    public JobServiceTests(SqliteTestFixture fixture) => _fixture = fixture;

    private async Task<int> CreateApprovedEstimateAsync(string suffix)
    {
        var costCodes = _fixture.GetRequiredService<ICostCodeService>();
        var cc = (await costCodes.GetAllAsync()).First(c => c.Code == "2000");
        var framing = (await costCodes.GetAllAsync()).First(c => c.Code == "3000");
        var estimateService = _fixture.GetRequiredService<IEstimateService>();

        var id = await estimateService.SaveAsync(new Estimate
        {
            EstimateNumber = $"EST-JOB-{suffix}",
            ProjectName = "123 Main Street"
        },
        [
            new EstimateLine { CostCodeId = cc.CostCodeId, Description = "Foundation", Quantity = 1, UnitCost = 35000m },
            new EstimateLine { CostCodeId = framing.CostCodeId, Description = "Framing", Quantity = 1, UnitCost = 72000m }
        ]);

        var estimate = await estimateService.GetByIdAsync(id);
        await estimateService.ApproveAsync(id, estimate!.Version);
        return id;
    }

    [Fact]
    public async Task CannotCreateJobFromDraftEstimate()
    {
        var costCodes = _fixture.GetRequiredService<ICostCodeService>();
        var cc = (await costCodes.GetAllAsync()).First();
        var estimateService = _fixture.GetRequiredService<IEstimateService>();
        var jobService = _fixture.GetRequiredService<IJobService>();

        var id = await estimateService.SaveAsync(new Estimate
        {
            EstimateNumber = $"EST-DRAFT-{Guid.NewGuid():N}",
            ProjectName = "Draft Project"
        },
        [
            new EstimateLine { CostCodeId = cc.CostCodeId, Quantity = 1, UnitCost = 100m }
        ]);

        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            jobService.CreateFromEstimateAsync(id, "JOB-DRAFT", "Draft Project"));
        Assert.Contains("approved", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ApprovedEstimateCreatesJob_WithBudgetSnapshot()
    {
        var estimateId = await CreateApprovedEstimateAsync(Guid.NewGuid().ToString("N"));
        var jobService = _fixture.GetRequiredService<IJobService>();
        var estimateService = _fixture.GetRequiredService<IEstimateService>();

        var jobId = await jobService.CreateFromEstimateAsync(estimateId, "JOB-1001", "123 Main Street");
        var budgetLines = await jobService.GetBudgetLinesAsync(jobId);
        var estimateTotal = await estimateService.GetTotalAsync(estimateId);

        Assert.Equal(107000m, budgetLines.Sum(l => l.BudgetAmount));
        Assert.Equal(estimateTotal, budgetLines.Sum(l => l.BudgetAmount));
    }

    [Fact]
    public async Task CannotCreateSecondJobFromSameEstimate()
    {
        var estimateId = await CreateApprovedEstimateAsync(Guid.NewGuid().ToString("N"));
        var jobService = _fixture.GetRequiredService<IJobService>();
        await jobService.CreateFromEstimateAsync(estimateId, $"JOB-{Guid.NewGuid():N}", "123 Main Street");

        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            jobService.CreateFromEstimateAsync(estimateId, $"JOB-{Guid.NewGuid():N}", "Duplicate"));
        Assert.Contains("already exists", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ClosedJobBlocksNewCostActivity()
    {
        var estimateId = await CreateApprovedEstimateAsync(Guid.NewGuid().ToString("N"));
        var jobService = _fixture.GetRequiredService<IJobService>();
        var jobCostService = _fixture.GetRequiredService<IJobCostService>();
        var costCodes = _fixture.GetRequiredService<ICostCodeService>();
        var cc = (await costCodes.GetAllAsync()).First();

        var jobId = await jobService.CreateFromEstimateAsync(estimateId, $"JOB-CLOSE-{Guid.NewGuid():N}", "Close Test");
        var job = await jobService.GetByIdAsync(jobId);
        await jobService.CloseAsync(jobId, job!.Version);

        var ex = await Assert.ThrowsAsync<ValidationException>(() => jobCostService.PostActualCostAsync(new JobCostTransaction
        {
            JobId = jobId,
            CostCodeId = cc.CostCodeId,
            TransactionDate = DateOnly.FromDateTime(DateTime.UtcNow),
            Amount = 100m
        }));
        Assert.Contains("active", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}
