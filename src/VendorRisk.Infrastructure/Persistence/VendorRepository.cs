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

    public async Task<VendorProfile> AddAsync(VendorProfile vendor, CancellationToken cancellationToken = default)
    {
        _dbContext.Vendors.Add(vendor);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return vendor;
    }

    public async Task UpdateAsync(VendorProfile vendor, CancellationToken cancellationToken = default)
    {
        _dbContext.Vendors.Update(vendor);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(VendorProfile vendor, CancellationToken cancellationToken = default)
    {
        _dbContext.Vendors.Remove(vendor);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
