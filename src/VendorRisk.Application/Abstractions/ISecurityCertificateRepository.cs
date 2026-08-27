using VendorRisk.Domain.Vendors;

namespace VendorRisk.Application.Abstractions;

/// <summary>Persistence boundary for the certificate catalogue. Implemented in Infrastructure.</summary>
public interface ISecurityCertificateRepository
{
    /// <summary>
    /// Normalises the supplied codes and returns the catalogue rows they name, registering any code
    /// the catalogue does not hold yet. Returns them in the caller's order, duplicates collapsed.
    /// A registered row is only staged, and is written when
    /// <see cref="IUnitOfWork.SaveChangesAsync"/> commits - until then its id is 0.
    /// </summary>
    Task<IReadOnlyList<SecurityCertificate>> ResolveAsync(
        IEnumerable<string>? codes,
        CancellationToken cancellationToken = default);
}
