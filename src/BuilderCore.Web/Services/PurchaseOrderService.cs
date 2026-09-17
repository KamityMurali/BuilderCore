using BuilderCore.Web.Data.Repositories;
using BuilderCore.Web.Hubs;
using BuilderCore.Web.Models;
using Microsoft.AspNetCore.SignalR;

namespace BuilderCore.Web.Services;

public interface IPurchaseOrderService
{
    Task<IReadOnlyList<PurchaseOrder>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PurchaseOrder>> GetByJobIdAsync(int jobId, CancellationToken cancellationToken = default);
    Task<PurchaseOrder?> GetByIdAsync(int purchaseOrderId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PurchaseOrderLine>> GetLinesAsync(int purchaseOrderId, CancellationToken cancellationToken = default);
    Task<decimal> GetTotalAsync(int purchaseOrderId, CancellationToken cancellationToken = default);
    Task<int> SaveAsync(PurchaseOrder purchaseOrder, IReadOnlyList<PurchaseOrderLine> lines, CancellationToken cancellationToken = default);
    Task IssueAsync(int purchaseOrderId, int version, CancellationToken cancellationToken = default);
    Task CloseAsync(int purchaseOrderId, int version, CancellationToken cancellationToken = default);
    Task<int> CountOpenAsync(CancellationToken cancellationToken = default);
}

public sealed class PurchaseOrderService(
    PurchaseOrderRepository repository,
    JobRepository jobRepository,
    VendorRepository vendorRepository,
    ICurrentUserService currentUser,
    IHubContext<JobCostHub> hubContext) : IPurchaseOrderService
{
    public Task<IReadOnlyList<PurchaseOrder>> GetAllAsync(CancellationToken cancellationToken = default)
        => repository.GetAllAsync(cancellationToken);

    public Task<IReadOnlyList<PurchaseOrder>> GetByJobIdAsync(int jobId, CancellationToken cancellationToken = default)
        => repository.GetByJobIdAsync(jobId, cancellationToken);

    public Task<PurchaseOrder?> GetByIdAsync(int purchaseOrderId, CancellationToken cancellationToken = default)
        => repository.GetByIdAsync(purchaseOrderId, cancellationToken);

    public Task<IReadOnlyList<PurchaseOrderLine>> GetLinesAsync(int purchaseOrderId, CancellationToken cancellationToken = default)
        => repository.GetLinesAsync(purchaseOrderId, cancellationToken);

    public Task<decimal> GetTotalAsync(int purchaseOrderId, CancellationToken cancellationToken = default)
        => repository.GetTotalAsync(purchaseOrderId, cancellationToken);

    public async Task<int> SaveAsync(PurchaseOrder purchaseOrder, IReadOnlyList<PurchaseOrderLine> lines, CancellationToken cancellationToken = default)
    {
        Validate(purchaseOrder, lines);
        await EnsureActiveJobAsync(purchaseOrder.JobId, cancellationToken);
        await EnsureActiveVendorAsync(purchaseOrder.VendorId, cancellationToken);

        if (await repository.NumberExistsAsync(purchaseOrder.PONumber.Trim(), purchaseOrder.PurchaseOrderId == 0 ? null : purchaseOrder.PurchaseOrderId, cancellationToken))
        {
            throw new ValidationException("PO number must be unique.");
        }

        purchaseOrder.PONumber = purchaseOrder.PONumber.Trim();

        if (purchaseOrder.PurchaseOrderId == 0)
        {
            var id = await repository.CreateAsync(purchaseOrder, currentUser.UserId, cancellationToken);
            await repository.ReplaceLinesAsync(id, lines, cancellationToken);
            return id;
        }

        var existing = await repository.GetByIdAsync(purchaseOrder.PurchaseOrderId, cancellationToken)
            ?? throw new ValidationException("Purchase order not found.");

        if (existing.Status != "Draft")
        {
            throw new ValidationException("Issued or closed purchase orders cannot be modified.");
        }

        await repository.UpdateHeaderAsync(purchaseOrder, currentUser.UserId, cancellationToken);
        await repository.ReplaceLinesAsync(purchaseOrder.PurchaseOrderId, lines, cancellationToken);
        return purchaseOrder.PurchaseOrderId;
    }

    public async Task IssueAsync(int purchaseOrderId, int version, CancellationToken cancellationToken = default)
    {
        var po = await repository.GetByIdAsync(purchaseOrderId, cancellationToken)
            ?? throw new ValidationException("Purchase order not found.");

        if (po.Status != "Draft")
        {
            throw new ValidationException("Only draft purchase orders can be issued.");
        }

        var lines = await repository.GetLinesAsync(purchaseOrderId, cancellationToken);
        if (lines.Count == 0)
        {
            throw new ValidationException("At least one line is required before issue.");
        }

        await repository.IssueAsync(purchaseOrderId, version, currentUser.UserId, cancellationToken);
        await hubContext.Clients.Group(JobCostHub.GroupName(po.JobId)).SendAsync("JobCostUpdated", new { jobId = po.JobId }, cancellationToken);
    }

    public async Task CloseAsync(int purchaseOrderId, int version, CancellationToken cancellationToken = default)
    {
        var po = await repository.GetByIdAsync(purchaseOrderId, cancellationToken)
            ?? throw new ValidationException("Purchase order not found.");

        if (po.Status != "Issued")
        {
            throw new ValidationException("Only issued purchase orders can be closed.");
        }

        await repository.CloseAsync(purchaseOrderId, version, currentUser.UserId, cancellationToken);
        await hubContext.Clients.Group(JobCostHub.GroupName(po.JobId)).SendAsync("JobCostUpdated", new { jobId = po.JobId }, cancellationToken);
    }

    public Task<int> CountOpenAsync(CancellationToken cancellationToken = default)
        => repository.CountOpenAsync(cancellationToken);

    private async Task EnsureActiveJobAsync(int jobId, CancellationToken cancellationToken)
    {
        var job = await jobRepository.GetByIdAsync(jobId, cancellationToken)
            ?? throw new ValidationException("Job not found.");

        if (job.Status != "Active")
        {
            throw new ValidationException("Purchase orders can only be created for active jobs.");
        }
    }

    private async Task EnsureActiveVendorAsync(int vendorId, CancellationToken cancellationToken)
    {
        var vendor = await vendorRepository.GetByIdAsync(vendorId, cancellationToken)
            ?? throw new ValidationException("Vendor not found.");

        if (!vendor.IsActive)
        {
            throw new ValidationException("Vendor must be active.");
        }
    }

    internal static void Validate(PurchaseOrder purchaseOrder, IReadOnlyList<PurchaseOrderLine> lines)
    {
        if (string.IsNullOrWhiteSpace(purchaseOrder.PONumber))
        {
            throw new ValidationException("PO number is required.");
        }

        if (purchaseOrder.JobId <= 0)
        {
            throw new ValidationException("Job is required.");
        }

        if (purchaseOrder.VendorId <= 0)
        {
            throw new ValidationException("Vendor is required.");
        }

        foreach (var line in lines)
        {
            if (line.CostCodeId <= 0)
            {
                throw new ValidationException("Each line requires a cost code.");
            }

            if (line.Amount <= 0)
            {
                throw new ValidationException("Line amount must be greater than zero.");
            }
        }
    }
}
