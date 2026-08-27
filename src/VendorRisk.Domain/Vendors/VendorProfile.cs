namespace VendorRisk.Domain.Vendors;

/// <summary>
/// A vendor and the assessment inputs described in section 4 of the case study.
/// </summary>
public class VendorProfile
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    /// <summary>Financial health score, 0-100. Higher is healthier.</summary>
    public int FinancialHealth { get; set; }

    /// <summary>SLA uptime percentage, 0-100.</summary>
    public decimal SlaUptime { get; set; }

    /// <summary>Major incidents recorded in the last 12 months.</summary>
    public int MajorIncidents { get; set; }

    /// <summary>
    /// Certificates this vendor holds, drawn from the shared catalogue and persisted through the
    /// vendor_certificates join table. Replace the set with <see cref="SetCertificates"/>.
    /// </summary>
    public List<SecurityCertificate> Certificates { get; set; } = [];

    /// <summary>
    /// Certificate codes in the shape section 4 of the case study defines, e.g. ["ISO27001"].
    /// Sorted, so the payload does not depend on the order the join table happens to return rows in.
    /// </summary>
    public IReadOnlyList<string> SecurityCerts =>
    [
        .. Certificates
            .Select(certificate => certificate.Code)
            .OrderBy(code => code, StringComparer.Ordinal)
    ];

    public VendorDocuments Documents { get; set; } = new();

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }

    /// <summary>Case-insensitive certificate lookup, so "iso27001" and "ISO27001" both match.</summary>
    public bool HasCertification(string certification) =>
        Certificates.Any(certificate =>
            string.Equals(certificate.Code, certification, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Replaces the vendor's certificates, dropping repeats of the same code so the join table can
    /// never hold a duplicate pair.
    /// </summary>
    public void SetCertificates(IEnumerable<SecurityCertificate>? certificates) =>
        Certificates =
        [
            .. (certificates ?? [])
                .Where(certificate => certificate is not null)
                .DistinctBy(certificate => certificate.Code, StringComparer.OrdinalIgnoreCase)
        ];
}
