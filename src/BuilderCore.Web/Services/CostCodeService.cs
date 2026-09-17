using BuilderCore.Web.Data.Repositories;
using BuilderCore.Web.Models;

namespace BuilderCore.Web.Services;

public interface ICostCodeService
{
    Task<IReadOnlyList<CostCode>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CostCode>> GetActiveAsync(CancellationToken cancellationToken = default);
    Task<CostCode?> GetByIdAsync(int costCodeId, CancellationToken cancellationToken = default);
    Task<int> SaveAsync(CostCode costCode, CancellationToken cancellationToken = default);
}

public sealed class CostCodeService(
    CostCodeRepository repository,
    ICurrentUserService currentUser) : ICostCodeService
{
    public Task<IReadOnlyList<CostCode>> GetAllAsync(CancellationToken cancellationToken = default)
        => repository.GetAllAsync(cancellationToken);

    public Task<IReadOnlyList<CostCode>> GetActiveAsync(CancellationToken cancellationToken = default)
        => repository.GetActiveAsync(cancellationToken);

    public Task<CostCode?> GetByIdAsync(int costCodeId, CancellationToken cancellationToken = default)
        => repository.GetByIdAsync(costCodeId, cancellationToken);

    public async Task<int> SaveAsync(CostCode costCode, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(costCode.Code))
        {
            throw new ValidationException("Code is required.");
        }

        if (string.IsNullOrWhiteSpace(costCode.Description))
        {
            throw new ValidationException("Description is required.");
        }

        if (await repository.CodeExistsAsync(costCode.Code.Trim(), costCode.CostCodeId == 0 ? null : costCode.CostCodeId, cancellationToken))
        {
            throw new ValidationException("Cost code must be unique.");
        }

        costCode.Code = costCode.Code.Trim();
        costCode.Description = costCode.Description.Trim();

        if (costCode.CostCodeId == 0)
        {
            return await repository.CreateAsync(costCode, currentUser.UserId, cancellationToken);
        }

        await repository.UpdateAsync(costCode, currentUser.UserId, cancellationToken);
        return costCode.CostCodeId;
    }
}
