namespace VendorRisk.Application.Dtos;

/// <summary>Document validity flags, matching the "documents" object in case study section 4.</summary>
public sealed class VendorDocumentsDto
{
    public bool ContractValid { get; set; }

    public bool PrivacyPolicyValid { get; set; }

    public bool PentestReportValid { get; set; }
}
