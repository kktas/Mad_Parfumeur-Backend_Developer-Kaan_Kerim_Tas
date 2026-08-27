namespace VendorRisk.Domain.Vendors;

/// <summary>
/// Compliance documents held for a vendor. Persisted as an owned type on <see cref="VendorProfile"/>.
/// </summary>
public class VendorDocuments
{
    public bool ContractValid { get; set; }

    public bool PrivacyPolicyValid { get; set; }

    public bool PentestReportValid { get; set; }
}
