using BuilderCore.Web.Data.Repositories;
using BuilderCore.Web.Hubs;
using BuilderCore.Web.Models;
using Microsoft.AspNetCore.SignalR;

namespace BuilderCore.Web.Services;

public interface IJobCostService
{
    Task<JobCostSummaryDto> GetSummaryAsync(int jobId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<JobCostTransaction>> GetTransactionsAsync(int jobId, CancellationToken cancellationToken = default);
    Task PostActualCostAsync(JobCostTransaction transaction, CancellationToken cancellationToken = default);
    Task<DashboardStats> GetDashboardStatsAsync(CancellationToken cancellationToken = default);
}

public sealed class JobCostService(
    JobCostRepository jobCostRepository,
    JobRepository jobRepository,
    EstimateRepository estimateRepository,
    PurchaseOrderRepository purchaseOrderRepository,
    ICurrentUserService currentUser,
    IHubContext<JobCostHub> hubContext) : IJobCostService
{
    public Task<JobCostSummaryDto> GetSummaryAsync(int jobId, CancellationToken cancellationToken = default)
        => jobCostRepository.GetSummaryAsync(jobId, cancellationToken);

    public Task<IReadOnlyList<JobCostTransaction>> GetTransactionsAsync(int jobId, CancellationToken cancellationToken = default)
        => jobCostRepository.GetTransactionsAsync(jobId, cancellationToken);

    public async Task PostActualCostAsync(JobCostTransaction transaction, CancellationToken cancellationToken = default)
    {
        if (transaction.Amount <= 0)
        {
            throw new ValidationException("Amount must be greater than zero.");
        }

        if (transaction.CostCodeId <= 0)
        {
            throw new ValidationException("Cost code is required.");
        }

        var job = await jobRepository.GetByIdAsync(transaction.JobId, cancellationToken)
            ?? throw new ValidationException("Job not found.");

        if (job.Status != "Active")
        {
            throw new ValidationException("Actual costs can only be posted to active jobs.");
        }

        if (transaction.PurchaseOrderId.HasValue)
        {
            var po = await purchaseOrderRepository.GetByIdAsync(transaction.PurchaseOrderId.Value, cancellationToken)
                ?? throw new ValidationException("Purchase order not found.");

            if (po.JobId != transaction.JobId)
            {
                throw new ValidationException("Purchase order must belong to the same job.");
            }
        }

        await jobCostRepository.CreateTransactionAsync(transaction, currentUser.UserId, cancellationToken);
        await hubContext.Clients.Group(JobCostHub.GroupName(transaction.JobId)).SendAsync("JobCostUpdated", new { jobId = transaction.JobId }, cancellationToken);
    }

    public async Task<DashboardStats> GetDashboardStatsAsync(CancellationToken cancellationToken = default)
    {
        var activeJobs = await jobRepository.CountByStatusAsync("Active", cancellationToken);
        var jobs = await jobRepository.GetAllAsync(cancellationToken);
        var recentSummaries = new List<JobCostSummaryDto>();

        foreach (var job in jobs.Take(5))
        {
            recentSummaries.Add(await jobCostRepository.GetSummaryAsync(job.JobId, cancellationToken));
        }

        return new DashboardStats
        {
            ActiveJobs = activeJobs,
            DraftEstimates = await estimateRepository.CountByStatusAsync("Draft", cancellationToken),
            OpenPurchaseOrders = await purchaseOrderRepository.CountOpenAsync(cancellationToken),
            RecentJobs = recentSummaries
        };
    }
}
