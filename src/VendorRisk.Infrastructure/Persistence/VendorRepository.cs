using Microsoft.EntityFrameworkCore;
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
        WithCertificates().FirstOrDefaultAsync(vendor => vendor.Id == id, cancellationToken);

    public async Task<IReadOnlyList<VendorProfile>> GetByIdsAsync(IReadOnlyCollection<int> ids, CancellationToken cancellationToken = default)
    {
        if (ids.Count == 0)
        {
            return [];
        }

        return await WithCertificates()
            .Where(vendor => ids.Contains(vendor.Id))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<VendorProfile>> ListAsync(int page, int pageSize, CancellationToken cancellationToken = default) =>
        await WithCertificates()
            .OrderBy(vendor => vendor.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

    /// <summary>
    /// Every read loads the certificates: the rules and the API response both need them, and a
    /// vendor without them would silently look like a vendor holding none. Split queries keep the
    /// join off the vendor columns.
    /// </summary>
    private IQueryable<VendorProfile> WithCertificates() =>
        _dbContext.Vendors
            .Include(vendor => vendor.Certificates)
            .AsSplitQuery();

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

    public void Add(VendorProfile vendor) => _dbContext.Vendors.Add(vendor);

    public void Update(VendorProfile vendor)
    {
        // The service loads the vendor before editing it, so the change tracker already holds the
        // edits, certificate links included. Re-attaching a tracked entity with Update() would also
        // mark the catalogue rows behind those links as modified.
        if (_dbContext.Entry(vendor).State == EntityState.Detached)
        {
            _dbContext.Vendors.Update(vendor);
        }
    }

    public void Remove(VendorProfile vendor) => _dbContext.Vendors.Remove(vendor);
}
