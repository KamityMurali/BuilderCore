using BuilderCore.Web.Models;
using BuilderCore.Web.Services;
using BuilderCore.Tests.TestFixtures;

namespace BuilderCore.Tests;

public class PurchaseOrderServiceTests : IClassFixture<SqliteTestFixture>
{
    private readonly SqliteTestFixture _fixture;

    public PurchaseOrderServiceTests(SqliteTestFixture fixture) => _fixture = fixture;

    private async Task<(int jobId, int vendorId, int framingCostCodeId)> CreateActiveJobAsync()
    {
        var costCodes = _fixture.GetRequiredService<ICostCodeService>();
        var foundation = (await costCodes.GetAllAsync()).First(c => c.Code == "2000");
        var framing = (await costCodes.GetAllAsync()).First(c => c.Code == "3000");
        var vendors = _fixture.GetRequiredService<IVendorService>();
        var vendor = (await vendors.GetAllAsync()).First();
        var estimateService = _fixture.GetRequiredService<IEstimateService>();
        var jobService = _fixture.GetRequiredService<IJobService>();

        var estimateId = await estimateService.SaveAsync(new Estimate
        {
            EstimateNumber = $"EST-PO-{Guid.NewGuid():N}",
            ProjectName = "PO Project"
        },
        [
            new EstimateLine { CostCodeId = foundation.CostCodeId, Quantity = 1, UnitCost = 35000m },
            new EstimateLine { CostCodeId = framing.CostCodeId, Quantity = 1, UnitCost = 72000m }
        ]);
        var estimate = await estimateService.GetByIdAsync(estimateId);
        await estimateService.ApproveAsync(estimateId, estimate!.Version);
        var jobId = await jobService.CreateFromEstimateAsync(estimateId, $"JOB-PO-{Guid.NewGuid():N}", "PO Project");
        return (jobId, vendor.VendorId, framing.CostCodeId);
    }

    [Fact]
    public async Task DraftPoExcluded_FromCommitted()
    {
        var (jobId, vendorId, framingCc) = await CreateActiveJobAsync();
        var poService = _fixture.GetRequiredService<IPurchaseOrderService>();
        var jobCostService = _fixture.GetRequiredService<IJobCostService>();

        await poService.SaveAsync(new PurchaseOrder
        {
            PONumber = $"PO-DRAFT-{Guid.NewGuid():N}",
            JobId = jobId,
            VendorId = vendorId
        },
        [
            new PurchaseOrderLine { CostCodeId = framingCc, Amount = 68000m }
        ]);

        var summary = await jobCostService.GetSummaryAsync(jobId);
        Assert.Equal(0m, summary.Committed);
    }

    [Fact]
    public async Task IssuedPoIncluded_InCommitted()
    {
        var (jobId, vendorId, framingCc) = await CreateActiveJobAsync();
        var poService = _fixture.GetRequiredService<IPurchaseOrderService>();
        var jobCostService = _fixture.GetRequiredService<IJobCostService>();

        var poId = await poService.SaveAsync(new PurchaseOrder
        {
            PONumber = $"PO-ISSUE-{Guid.NewGuid():N}",
            JobId = jobId,
            VendorId = vendorId
        },
        [
            new PurchaseOrderLine { CostCodeId = framingCc, Amount = 68000m }
        ]);

        var po = await poService.GetByIdAsync(poId);
        await poService.IssueAsync(poId, po!.Version);
        var summary = await jobCostService.GetSummaryAsync(jobId);
        Assert.Equal(68000m, summary.Committed);
    }

    [Fact]
    public async Task ClosedPoRemainsIncluded_InCommitted()
    {
        var (jobId, vendorId, framingCc) = await CreateActiveJobAsync();
        var poService = _fixture.GetRequiredService<IPurchaseOrderService>();
        var jobCostService = _fixture.GetRequiredService<IJobCostService>();

        var poId = await poService.SaveAsync(new PurchaseOrder
        {
            PONumber = $"PO-CLOSE-{Guid.NewGuid():N}",
            JobId = jobId,
            VendorId = vendorId
        },
        [
            new PurchaseOrderLine { CostCodeId = framingCc, Amount = 68000m }
        ]);

        var po = await poService.GetByIdAsync(poId);
        await poService.IssueAsync(poId, po!.Version);
        po = await poService.GetByIdAsync(poId);
        await poService.CloseAsync(poId, po!.Version);

        var summary = await jobCostService.GetSummaryAsync(jobId);
        Assert.Equal(68000m, summary.Committed);
    }

    [Fact]
    public async Task CannotIssueEmptyPo()
    {
        var (jobId, vendorId, _) = await CreateActiveJobAsync();
        var poService = _fixture.GetRequiredService<IPurchaseOrderService>();

        var poId = await poService.SaveAsync(new PurchaseOrder
        {
            PONumber = $"PO-EMPTY-{Guid.NewGuid():N}",
            JobId = jobId,
            VendorId = vendorId
        }, []);

        var po = await poService.GetByIdAsync(poId);
        var ex = await Assert.ThrowsAsync<ValidationException>(() => poService.IssueAsync(poId, po!.Version));
        Assert.Contains("At least one line", ex.Message);
    }

    [Fact]
    public async Task CannotModifyIssuedPo()
    {
        var (jobId, vendorId, framingCc) = await CreateActiveJobAsync();
        var poService = _fixture.GetRequiredService<IPurchaseOrderService>();

        var poId = await poService.SaveAsync(new PurchaseOrder
        {
            PONumber = $"PO-MOD-{Guid.NewGuid():N}",
            JobId = jobId,
            VendorId = vendorId
        },
        [
            new PurchaseOrderLine { CostCodeId = framingCc, Amount = 1000m }
        ]);

        var po = await poService.GetByIdAsync(poId);
        await poService.IssueAsync(poId, po!.Version);
        po = await poService.GetByIdAsync(poId);

        var ex = await Assert.ThrowsAsync<ValidationException>(() => poService.SaveAsync(po!, [
            new PurchaseOrderLine { CostCodeId = framingCc, Amount = 2000m }
        ]));
        Assert.Contains("cannot be modified", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CannotCreatePoForClosedJob()
    {
        var (jobId, vendorId, framingCc) = await CreateActiveJobAsync();
        var jobService = _fixture.GetRequiredService<IJobService>();
        var poService = _fixture.GetRequiredService<IPurchaseOrderService>();

        var job = await jobService.GetByIdAsync(jobId);
        await jobService.CloseAsync(jobId, job!.Version);

        var ex = await Assert.ThrowsAsync<ValidationException>(() => poService.SaveAsync(new PurchaseOrder
        {
            PONumber = $"PO-CLOSEDJOB-{Guid.NewGuid():N}",
            JobId = jobId,
            VendorId = vendorId
        },
        [
            new PurchaseOrderLine { CostCodeId = framingCc, Amount = 1000m }
        ]));
        Assert.Contains("active", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}
