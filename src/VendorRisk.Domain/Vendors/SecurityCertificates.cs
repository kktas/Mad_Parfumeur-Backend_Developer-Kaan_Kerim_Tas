namespace VendorRisk.Domain.Vendors;

/// <summary>
/// Canonical form for security certificate names. Certificates are stored upper-cased and
/// de-duplicated so that "ISO27001", "iso27001" and a repeated entry all collapse to one value,
/// which keeps rule evaluation and the API payload predictable.
/// </summary>
public static class SecurityCertificates
{
    /// <summary>
    /// Trims each entry, drops blanks, upper-cases, and removes duplicates while preserving the
    /// order the caller supplied.
    /// </summary>
    public static List<string> Normalise(IEnumerable<string>? certifications) =>
    [
        .. (certifications ?? [])
            .Where(certification => !string.IsNullOrWhiteSpace(certification))
            .Select(certification => certification.Trim().ToUpperInvariant())
            .Distinct(StringComparer.Ordinal)
    ];
}
