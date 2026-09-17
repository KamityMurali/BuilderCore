using BuilderCore.Web.Models;
using BuilderCore.Web.Services;
using BuilderCore.Tests.TestFixtures;

namespace BuilderCore.Tests;

public class EstimateServiceTests : IClassFixture<SqliteTestFixture>
{
    private readonly SqliteTestFixture _fixture;

    public EstimateServiceTests(SqliteTestFixture fixture) => _fixture = fixture;

    [Fact]
    public void LineAmount_IsQuantityTimesUnitCost()
    {
        var line = new EstimateLine { Quantity = 2, UnitCost = 35000m };
        Assert.Equal(70000m, line.LineAmount);
    }

    [Fact]
    public void Total_IsSumOfLineAmounts()
    {
        var lines = new List<EstimateLine>
        {
            new() { Quantity = 1, UnitCost = 35000m },
            new() { Quantity = 1, UnitCost = 72000m }
        };

        Assert.Equal(107000m, lines.Sum(l => l.LineAmount));
    }

    [Fact]
    public async Task CannotApproveEmptyEstimate()
    {
        var service = _fixture.GetRequiredService<IEstimateService>();
        var id = await service.SaveAsync(new Estimate
        {
            EstimateNumber = "EST-EMPTY",
            ProjectName = "Empty Project"
        }, []);

        var estimate = await service.GetByIdAsync(id);
        var ex = await Assert.ThrowsAsync<ValidationException>(() => service.ApproveAsync(id, estimate!.Version));
        Assert.Contains("At least one line", ex.Message);
    }

    [Fact]
    public async Task ApproveValidEstimate_Succeeds()
    {
        var costCodes = _fixture.GetRequiredService<ICostCodeService>();
        var cc = (await costCodes.GetAllAsync()).First();

        var service = _fixture.GetRequiredService<IEstimateService>();
        var id = await service.SaveAsync(new Estimate
        {
            EstimateNumber = $"EST-APPROVE-{Guid.NewGuid():N}",
            ProjectName = "Approve Project"
        },
        [
            new EstimateLine { CostCodeId = cc.CostCodeId, Quantity = 1, UnitCost = 1000m }
        ]);

        var estimate = await service.GetByIdAsync(id);
        await service.ApproveAsync(id, estimate!.Version);
        estimate = await service.GetByIdAsync(id);
        Assert.Equal("Approved", estimate!.Status);
    }

    [Fact]
    public async Task CannotEditApprovedEstimate()
    {
        var costCodes = _fixture.GetRequiredService<ICostCodeService>();
        var cc = (await costCodes.GetAllAsync()).First();
        var service = _fixture.GetRequiredService<IEstimateService>();

        var id = await service.SaveAsync(new Estimate
        {
            EstimateNumber = $"EST-RO-{Guid.NewGuid():N}",
            ProjectName = "Read Only Project"
        },
        [
            new EstimateLine { CostCodeId = cc.CostCodeId, Quantity = 1, UnitCost = 500m }
        ]);

        var estimate = await service.GetByIdAsync(id);
        await service.ApproveAsync(id, estimate!.Version);
        estimate = await service.GetByIdAsync(id);

        var ex = await Assert.ThrowsAsync<ValidationException>(() => service.SaveAsync(new Estimate
        {
            EstimateId = id,
            EstimateNumber = estimate!.EstimateNumber,
            ProjectName = "Changed",
            Status = "Approved",
            Version = estimate.Version
        }, []));
        Assert.Contains("cannot be edited", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}
