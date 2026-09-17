using BuilderCore.Web.Data.Repositories;
using BuilderCore.Web.Models;

namespace BuilderCore.Web.Services;

public interface IJobService
{
    Task<IReadOnlyList<Job>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<Job?> GetByIdAsync(int jobId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<JobBudgetLine>> GetBudgetLinesAsync(int jobId, CancellationToken cancellationToken = default);
    Task<int> CreateFromEstimateAsync(int estimateId, string jobNumber, string jobName, CancellationToken cancellationToken = default);
    Task CloseAsync(int jobId, int version, CancellationToken cancellationToken = default);
    Task<int> CountActiveAsync(CancellationToken cancellationToken = default);
}

public sealed class JobService(
    JobRepository jobRepository,
    EstimateRepository estimateRepository,
    ICurrentUserService currentUser) : IJobService
{
    public Task<IReadOnlyList<Job>> GetAllAsync(CancellationToken cancellationToken = default)
        => jobRepository.GetAllAsync(cancellationToken);

    public Task<Job?> GetByIdAsync(int jobId, CancellationToken cancellationToken = default)
        => jobRepository.GetByIdAsync(jobId, cancellationToken);

    public Task<IReadOnlyList<JobBudgetLine>> GetBudgetLinesAsync(int jobId, CancellationToken cancellationToken = default)
        => jobRepository.GetBudgetLinesAsync(jobId, cancellationToken);

    public async Task<int> CreateFromEstimateAsync(int estimateId, string jobNumber, string jobName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(jobNumber))
        {
            throw new ValidationException("Job number is required.");
        }

        if (string.IsNullOrWhiteSpace(jobName))
        {
            throw new ValidationException("Job name is required.");
        }

        var estimate = await estimateRepository.GetByIdAsync(estimateId, cancellationToken)
            ?? throw new ValidationException("Estimate not found.");

        if (estimate.Status != "Approved")
        {
            throw new ValidationException("A job can only be created from an approved estimate.");
        }

        if (estimate.JobId.HasValue)
        {
            throw new ValidationException("A job already exists for this estimate.");
        }

        if (await jobRepository.NumberExistsAsync(jobNumber.Trim(), cancellationToken))
        {
            throw new ValidationException("Job number must be unique.");
        }

        var lines = await estimateRepository.GetLinesAsync(estimateId, cancellationToken);
        if (lines.Count == 0)
        {
            throw new ValidationException("Estimate has no lines to snapshot.");
        }

        return await jobRepository.CreateFromEstimateAsync(
            jobNumber.Trim(),
            jobName.Trim(),
            estimateId,
            lines,
            currentUser.UserId,
            cancellationToken);
    }

    public async Task CloseAsync(int jobId, int version, CancellationToken cancellationToken = default)
    {
        var job = await jobRepository.GetByIdAsync(jobId, cancellationToken)
            ?? throw new ValidationException("Job not found.");

        if (job.Status != "Active")
        {
            throw new ValidationException("Only active jobs can be closed.");
        }

        await jobRepository.CloseAsync(jobId, version, currentUser.UserId, cancellationToken);
    }

    public Task<int> CountActiveAsync(CancellationToken cancellationToken = default)
        => jobRepository.CountByStatusAsync("Active", cancellationToken);
}
