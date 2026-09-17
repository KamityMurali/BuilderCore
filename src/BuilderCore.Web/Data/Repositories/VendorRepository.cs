using BuilderCore.Web.Models;
using Dapper;

namespace BuilderCore.Web.Data.Repositories;

public sealed class VendorRepository(ISqliteConnectionFactory connectionFactory)
{
    public async Task<IReadOnlyList<Vendor>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<Vendor>(
            new CommandDefinition(
                """
                SELECT VendorId, VendorName, Email, Phone, IsActive
                FROM Vendor
                ORDER BY VendorName;
                """,
                cancellationToken: cancellationToken));
        return rows.AsList();
    }

    public async Task<IReadOnlyList<Vendor>> GetActiveAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<Vendor>(
            new CommandDefinition(
                """
                SELECT VendorId, VendorName, Email, Phone, IsActive
                FROM Vendor
                WHERE IsActive = 1
                ORDER BY VendorName;
                """,
                cancellationToken: cancellationToken));
        return rows.AsList();
    }

    public async Task<Vendor?> GetByIdAsync(int vendorId, CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<Vendor>(
            new CommandDefinition(
                """
                SELECT VendorId, VendorName, Email, Phone, IsActive
                FROM Vendor
                WHERE VendorId = @VendorId;
                """,
                new { VendorId = vendorId },
                cancellationToken: cancellationToken));
    }

    public async Task<int> CreateAsync(Vendor vendor, string userId, CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(
                """
                INSERT INTO Vendor (VendorName, Email, Phone, IsActive, CreatedBy, CreatedDateUtc)
                VALUES (@VendorName, @Email, @Phone, @IsActive, @CreatedBy, @CreatedDateUtc);
                SELECT last_insert_rowid();
                """,
                new
                {
                    vendor.VendorName,
                    vendor.Email,
                    vendor.Phone,
                    IsActive = vendor.IsActive ? 1 : 0,
                    CreatedBy = userId,
                    CreatedDateUtc = DateTime.UtcNow.ToString("O")
                },
                cancellationToken: cancellationToken));
    }

    public async Task UpdateAsync(Vendor vendor, string userId, CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(
            new CommandDefinition(
                """
                UPDATE Vendor
                SET VendorName = @VendorName,
                    Email = @Email,
                    Phone = @Phone,
                    IsActive = @IsActive,
                    UpdatedBy = @UpdatedBy,
                    UpdatedDateUtc = @UpdatedDateUtc
                WHERE VendorId = @VendorId;
                """,
                new
                {
                    vendor.VendorId,
                    vendor.VendorName,
                    vendor.Email,
                    vendor.Phone,
                    IsActive = vendor.IsActive ? 1 : 0,
                    UpdatedBy = userId,
                    UpdatedDateUtc = DateTime.UtcNow.ToString("O")
                },
                cancellationToken: cancellationToken));
    }

    public async Task<bool> NameExistsAsync(string vendorName, int? excludeId, CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var count = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(
                """
                SELECT COUNT(1) FROM Vendor
                WHERE VendorName = @VendorName AND (@ExcludeId IS NULL OR VendorId <> @ExcludeId);
                """,
                new { VendorName = vendorName, ExcludeId = excludeId },
                cancellationToken: cancellationToken));
        return count > 0;
    }
}
