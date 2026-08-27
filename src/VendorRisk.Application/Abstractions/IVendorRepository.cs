using VendorRisk.Domain.Vendors;

namespace VendorRisk.Application.Abstractions;

/// <summary>Persistence boundary for vendors. Implemented in the Infrastructure layer.</summary>
public interface IVendorRepository
{
    Task<VendorProfile?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<VendorProfile>> GetByIdsAsync(IReadOnlyCollection<int> ids, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<VendorProfile>> ListAsync(int page, int pageSize, CancellationToken cancellationToken = default);

    Task<int> CountAsync(CancellationToken cancellationToken = default);

    Task<VendorProfile> AddAsync(VendorProfile vendor, CancellationToken cancellationToken = default);

    Task UpdateAsync(VendorProfile vendor, CancellationToken cancellationToken = default);

    Task DeleteAsync(VendorProfile vendor, CancellationToken cancellationToken = default);
}
