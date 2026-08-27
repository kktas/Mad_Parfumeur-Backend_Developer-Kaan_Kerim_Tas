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
    /// Security certifications held, e.g. ISO27001, SOC2, PCI-DSS. Stored upper-cased and
    /// de-duplicated; see <see cref="SecurityCertificates.Normalise"/>.
    /// </summary>
    public List<string> SecurityCerts { get; set; } = [];

    public VendorDocuments Documents { get; set; } = new();

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }

    /// <summary>Case-insensitive certificate lookup, so "iso27001" and "ISO27001" both match.</summary>
    public bool HasCertification(string certification) =>
        SecurityCerts.Any(cert => string.Equals(cert, certification, StringComparison.OrdinalIgnoreCase));
}
