using BuilderCore.Web.Data.Repositories;
using BuilderCore.Web.Models;

namespace BuilderCore.Web.Services;

public interface IEstimateService
{
    Task<IReadOnlyList<Estimate>> GetAllAsync(string? search, CancellationToken cancellationToken = default);
    Task<Estimate?> GetByIdAsync(int estimateId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<EstimateLine>> GetLinesAsync(int estimateId, CancellationToken cancellationToken = default);
    Task<decimal> GetTotalAsync(int estimateId, CancellationToken cancellationToken = default);
    Task<int> SaveAsync(Estimate estimate, IReadOnlyList<EstimateLine> lines, CancellationToken cancellationToken = default);
    Task ApproveAsync(int estimateId, int version, CancellationToken cancellationToken = default);
    Task<int> CountDraftAsync(CancellationToken cancellationToken = default);
}

public sealed class EstimateService(
    EstimateRepository repository,
    ICurrentUserService currentUser) : IEstimateService
{
    public Task<IReadOnlyList<Estimate>> GetAllAsync(string? search, CancellationToken cancellationToken = default)
        => repository.GetAllAsync(search, cancellationToken);

    public Task<Estimate?> GetByIdAsync(int estimateId, CancellationToken cancellationToken = default)
        => repository.GetByIdAsync(estimateId, cancellationToken);

    public Task<IReadOnlyList<EstimateLine>> GetLinesAsync(int estimateId, CancellationToken cancellationToken = default)
        => repository.GetLinesAsync(estimateId, cancellationToken);

    public Task<decimal> GetTotalAsync(int estimateId, CancellationToken cancellationToken = default)
        => repository.GetTotalAsync(estimateId, cancellationToken);

    public async Task<int> SaveAsync(Estimate estimate, IReadOnlyList<EstimateLine> lines, CancellationToken cancellationToken = default)
    {
        ValidateHeader(estimate);
        ValidateLines(lines);

        if (await repository.NumberExistsAsync(estimate.EstimateNumber.Trim(), estimate.EstimateId == 0 ? null : estimate.EstimateId, cancellationToken))
        {
            throw new ValidationException("Estimate number must be unique.");
        }

        estimate.EstimateNumber = estimate.EstimateNumber.Trim();
        estimate.ProjectName = estimate.ProjectName.Trim();

        if (estimate.EstimateId == 0)
        {
            estimate.Status = "Draft";
            var id = await repository.CreateAsync(estimate, currentUser.UserId, cancellationToken);
            await repository.ReplaceLinesAsync(id, lines, cancellationToken);
            return id;
        }

        var existing = await repository.GetByIdAsync(estimate.EstimateId, cancellationToken)
            ?? throw new ValidationException("Estimate not found.");

        if (existing.Status != "Draft")
        {
            throw new ValidationException("Approved estimates cannot be edited.");
        }

        await repository.UpdateHeaderAsync(estimate, currentUser.UserId, cancellationToken);
        await repository.ReplaceLinesAsync(estimate.EstimateId, lines, cancellationToken);
        return estimate.EstimateId;
    }

    public async Task ApproveAsync(int estimateId, int version, CancellationToken cancellationToken = default)
    {
        var estimate = await repository.GetByIdAsync(estimateId, cancellationToken)
            ?? throw new ValidationException("Estimate not found.");

        if (estimate.Status != "Draft")
        {
            throw new ValidationException("Only draft estimates can be approved.");
        }

        var lines = await repository.GetLinesAsync(estimateId, cancellationToken);
        if (lines.Count == 0)
        {
            throw new ValidationException("At least one line is required before approval.");
        }

        await repository.ApproveAsync(estimateId, version, currentUser.UserId, cancellationToken);
    }

    public Task<int> CountDraftAsync(CancellationToken cancellationToken = default)
        => repository.CountByStatusAsync("Draft", cancellationToken);

    internal static void ValidateHeader(Estimate estimate)
    {
        if (string.IsNullOrWhiteSpace(estimate.EstimateNumber))
        {
            throw new ValidationException("Estimate number is required.");
        }

        if (string.IsNullOrWhiteSpace(estimate.ProjectName))
        {
            throw new ValidationException("Project name is required.");
        }
    }

    internal static void ValidateLines(IReadOnlyList<EstimateLine> lines)
    {
        foreach (var line in lines)
        {
            if (line.CostCodeId <= 0)
            {
                throw new ValidationException("Each line requires a cost code.");
            }

            if (line.Quantity <= 0)
            {
                throw new ValidationException("Quantity must be greater than zero.");
            }

            if (line.UnitCost < 0)
            {
                throw new ValidationException("Unit cost cannot be negative.");
            }
        }
    }

    internal static decimal CalculateTotal(IReadOnlyList<EstimateLine> lines)
        => lines.Sum(l => l.Quantity * l.UnitCost);
}
