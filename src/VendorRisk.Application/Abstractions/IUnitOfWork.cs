namespace VendorRisk.Application.Abstractions;

/// <summary>
/// The commit boundary for a single operation. Repositories only stage work - adding, updating and
/// removing entities - and nothing reaches the database until this is called, so everything an
/// operation touches lands in one transaction or not at all.
/// </summary>
/// <remarks>
/// EF Core's DbContext is already a unit of work. This interface exists so the application layer
/// can commit without referencing EF Core, and so no repository saves behind the service's back.
/// </remarks>
public interface IUnitOfWork
{
    /// <summary>
    /// Commits everything staged since the last call and returns the number of rows written.
    /// </summary>
    /// <exception cref="Domain.Vendors.DuplicateVendorNameException">
    /// Another vendor holds the name, caught at the unique index rather than the service's check.
    /// </exception>
    /// <exception cref="Domain.Vendors.DuplicateCertificateCodeException">
    /// Another request registered the same new certificate code first.
    /// </exception>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
