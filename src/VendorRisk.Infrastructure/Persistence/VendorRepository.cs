using Microsoft.EntityFrameworkCore;
using Npgsql;
using VendorRisk.Application.Abstractions;
using VendorRisk.Domain.Vendors;

namespace VendorRisk.Infrastructure.Persistence;

public sealed class VendorRepository : IVendorRepository
{
    private readonly VendorRiskDbContext _dbContext;

    public VendorRepository(VendorRiskDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public Task<VendorProfile?> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
        _dbContext.Vendors.FirstOrDefaultAsync(vendor => vendor.Id == id, cancellationToken);

    public async Task<IReadOnlyList<VendorProfile>> GetByIdsAsync(IReadOnlyCollection<int> ids, CancellationToken cancellationToken = default)
    {
        if (ids.Count == 0)
        {
            return [];
        }

        return await _dbContext.Vendors
            .Where(vendor => ids.Contains(vendor.Id))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<VendorProfile>> ListAsync(int page, int pageSize, CancellationToken cancellationToken = default) =>
        await _dbContext.Vendors
            .OrderBy(vendor => vendor.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

    public Task<int> CountAsync(CancellationToken cancellationToken = default) =>
        _dbContext.Vendors.CountAsync(cancellationToken);

    public Task<bool> NameExistsAsync(string name, int? excludeVendorId = null, CancellationToken cancellationToken = default)
    {
        // ToLower translates to Postgres lower(), matching the unique functional index on the table.
        var normalised = name.Trim().ToLower();

        return _dbContext.Vendors.AnyAsync(
            vendor => vendor.Name.ToLower() == normalised && (excludeVendorId == null || vendor.Id != excludeVendorId),
            cancellationToken);
    }

    public async Task<VendorProfile> AddAsync(VendorProfile vendor, CancellationToken cancellationToken = default)
    {
        _dbContext.Vendors.Add(vendor);
        await SaveTranslatingDuplicateNameAsync(vendor.Name, cancellationToken);

        return vendor;
    }

    public async Task UpdateAsync(VendorProfile vendor, CancellationToken cancellationToken = default)
    {
        _dbContext.Vendors.Update(vendor);
        await SaveTranslatingDuplicateNameAsync(vendor.Name, cancellationToken);
    }

    /// <summary>
    /// The service checks for a duplicate name before saving, but two concurrent requests can both
    /// pass that check. The unique index is the real guard, so translate its violation into the
    /// same domain exception rather than surfacing a 500.
    /// </summary>
    private async Task SaveTranslatingDuplicateNameAsync(string vendorName, CancellationToken cancellationToken)
    {
        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            throw new DuplicateVendorNameException(vendorName);
        }
    }

    public async Task DeleteAsync(VendorProfile vendor, CancellationToken cancellationToken = default)
    {
        _dbContext.Vendors.Remove(vendor);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
