namespace VendorRisk.Domain.Vendors;

/// <summary>
/// Join entity between <see cref="VendorProfile"/> and <see cref="SecurityCertificate"/>, mapped to
/// the vendor_certificates table. Declared explicitly rather than left to EF's implicit join type so
/// the relationship can be queried and configured directly.
/// </summary>
public class VendorCertificate
{
    public int VendorId { get; set; }

    public VendorProfile Vendor { get; set; } = null!;

    public int CertificateId { get; set; }

    public SecurityCertificate Certificate { get; set; } = null!;
}
