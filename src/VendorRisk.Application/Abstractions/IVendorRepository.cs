using VendorRisk.Domain.Vendors;

namespace VendorRisk.Application.Abstractions;

/// <summary>
/// Persistence boundary for vendors. Implemented in the Infrastructure layer.
/// </summary>
/// <remarks>
/// The write methods only stage work; nothing is written until
/// <see cref="IUnitOfWork.SaveChangesAsync"/> commits it.
/// </remarks>
public interface IVendorRepository
{
    Task<VendorProfile?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<VendorProfile>> GetByIdsAsync(IReadOnlyCollection<int> ids, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<VendorProfile>> ListAsync(int page, int pageSize, CancellationToken cancellationToken = default);

    Task<int> CountAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Whether another vendor already holds this name, compared case-insensitively.
    /// </summary>
    /// <param name="excludeVendorId">
    /// Vendor to ignore, so an update that keeps its own name does not collide with itself.
    /// </param>
    Task<bool> NameExistsAsync(string name, int? excludeVendorId = null, CancellationToken cancellationToken = default);

    /// <summary>Stages a new vendor. Its id is assigned when the unit of work commits.</summary>
    void Add(VendorProfile vendor);

    /// <summary>Stages the edits made to a vendor this repository returned.</summary>
    void Update(VendorProfile vendor);

    /// <summary>Stages the vendor's removal, along with its certificate links.</summary>
    void Remove(VendorProfile vendor);
}
