using BuilderCore.Web.Models;
using BuilderCore.Web.Services;
using BuilderCore.Tests.TestFixtures;

namespace BuilderCore.Tests;

public class ConcurrencyTests : IClassFixture<SqliteTestFixture>
{
    private readonly SqliteTestFixture _fixture;

    public ConcurrencyTests(SqliteTestFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task StaleEstimateVersion_ReturnsConcurrencyConflict()
    {
        var costCodes = _fixture.GetRequiredService<ICostCodeService>();
        var cc = (await costCodes.GetAllAsync()).First();
        var service = _fixture.GetRequiredService<IEstimateService>();

        var id = await service.SaveAsync(new Estimate
        {
            EstimateNumber = $"EST-CONC-{Guid.NewGuid():N}",
            ProjectName = "Concurrency Project"
        },
        [
            new EstimateLine { CostCodeId = cc.CostCodeId, Quantity = 1, UnitCost = 100m }
        ]);

        var ex = await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() =>
            service.ApproveAsync(id, 999));
        Assert.Equal(DbUpdateConcurrencyException.UserMessage, ex.Message);
    }

    [Fact]
    public async Task StalePurchaseOrderVersion_ReturnsConcurrencyConflict()
    {
        var costCodes = _fixture.GetRequiredService<ICostCodeService>();
        var cc = (await costCodes.GetAllAsync()).First(c => c.Code == "3000");
        var vendors = _fixture.GetRequiredService<IVendorService>();
        var vendor = (await vendors.GetAllAsync()).First();
        var estimateService = _fixture.GetRequiredService<IEstimateService>();
        var jobService = _fixture.GetRequiredService<IJobService>();
        var poService = _fixture.GetRequiredService<IPurchaseOrderService>();

        var estimateId = await estimateService.SaveAsync(new Estimate
        {
            EstimateNumber = $"EST-POCONC-{Guid.NewGuid():N}",
            ProjectName = "PO Concurrency"
        },
        [
            new EstimateLine { CostCodeId = cc.CostCodeId, Quantity = 1, UnitCost = 1000m }
        ]);
        var estimate = await estimateService.GetByIdAsync(estimateId);
        await estimateService.ApproveAsync(estimateId, estimate!.Version);
        var jobId = await jobService.CreateFromEstimateAsync(estimateId, $"JOB-POCONC-{Guid.NewGuid():N}", "PO Concurrency");

        var poId = await poService.SaveAsync(new PurchaseOrder
        {
            PONumber = $"PO-CONC-{Guid.NewGuid():N}",
            JobId = jobId,
            VendorId = vendor.VendorId
        },
        [
            new PurchaseOrderLine { CostCodeId = cc.CostCodeId, Amount = 500m }
        ]);

        var ex = await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() =>
            poService.IssueAsync(poId, 999));
        Assert.Equal(DbUpdateConcurrencyException.UserMessage, ex.Message);
    }
}
