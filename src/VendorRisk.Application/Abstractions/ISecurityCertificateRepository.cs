using VendorRisk.Domain.Vendors;

namespace VendorRisk.Application.Abstractions;

/// <summary>Persistence boundary for the certificate catalogue. Implemented in Infrastructure.</summary>
public interface ISecurityCertificateRepository
{
    /// <summary>
    /// Normalises the supplied codes and returns the catalogue rows they name, registering any code
    /// the catalogue does not hold yet. Returns them in the caller's order, duplicates collapsed.
    /// </summary>
    Task<IReadOnlyList<SecurityCertificate>> ResolveAsync(
        IEnumerable<string>? codes,
        CancellationToken cancellationToken = default);
}
