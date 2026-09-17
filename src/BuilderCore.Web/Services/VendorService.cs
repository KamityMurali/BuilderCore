using System.Net.Mail;
using BuilderCore.Web.Data.Repositories;
using BuilderCore.Web.Models;

namespace BuilderCore.Web.Services;

public interface IVendorService
{
    Task<IReadOnlyList<Vendor>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Vendor>> GetActiveAsync(CancellationToken cancellationToken = default);
    Task<Vendor?> GetByIdAsync(int vendorId, CancellationToken cancellationToken = default);
    Task<int> SaveAsync(Vendor vendor, CancellationToken cancellationToken = default);
}

public sealed class VendorService(
    VendorRepository repository,
    ICurrentUserService currentUser) : IVendorService
{
    public Task<IReadOnlyList<Vendor>> GetAllAsync(CancellationToken cancellationToken = default)
        => repository.GetAllAsync(cancellationToken);

    public Task<IReadOnlyList<Vendor>> GetActiveAsync(CancellationToken cancellationToken = default)
        => repository.GetActiveAsync(cancellationToken);

    public Task<Vendor?> GetByIdAsync(int vendorId, CancellationToken cancellationToken = default)
        => repository.GetByIdAsync(vendorId, cancellationToken);

    public async Task<int> SaveAsync(Vendor vendor, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(vendor.VendorName))
        {
            throw new ValidationException("Vendor name is required.");
        }

        if (!string.IsNullOrWhiteSpace(vendor.Email) && !IsValidEmail(vendor.Email))
        {
            throw new ValidationException("Email is not valid.");
        }

        vendor.VendorName = vendor.VendorName.Trim();

        if (vendor.VendorId == 0)
        {
            return await repository.CreateAsync(vendor, currentUser.UserId, cancellationToken);
        }

        await repository.UpdateAsync(vendor, currentUser.UserId, cancellationToken);
        return vendor.VendorId;
    }

    private static bool IsValidEmail(string email)
    {
        try
        {
            _ = new MailAddress(email);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
