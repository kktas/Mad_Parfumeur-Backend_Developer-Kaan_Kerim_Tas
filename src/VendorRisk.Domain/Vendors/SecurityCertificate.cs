namespace VendorRisk.Domain.Vendors;

/// <summary>
/// A security or compliance certification a vendor can hold, e.g. ISO27001. Certificates live in
/// their own catalogue table rather than as free text on the vendor, so the name and description of
/// a certification are stated once and shared by every vendor that holds it.
/// </summary>
public class SecurityCertificate
{
    public int Id { get; set; }

    /// <summary>
    /// Canonical code, upper-cased and unique across the catalogue - the value the rules match on
    /// and the value the API contract carries in <c>securityCerts</c>. See
    /// <see cref="SecurityCertificates.Normalise"/>.
    /// </summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>Human-readable name, e.g. "ISO/IEC 27001 Information Security Management".</summary>
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    /// <summary>Vendors holding this certificate, through the vendor_certificates join table.</summary>
    public List<VendorProfile> Vendors { get; set; } = [];
}
