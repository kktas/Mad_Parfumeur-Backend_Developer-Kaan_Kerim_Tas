using System.Text.Json.Serialization;

namespace VendorRisk.Infrastructure.Seeding;

/// <summary>Shape of data/SampleVendorData.json (case study appendix B).</summary>
public sealed class SampleVendorDataFile
{
    [JsonPropertyName("Vendors")]
    public List<SampleVendorRecord> Vendors { get; set; } = [];
}

public sealed class SampleVendorRecord
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public int FinancialHealth { get; set; }

    public decimal SlaUptime { get; set; }

    public int MajorIncidents { get; set; }

    public List<string> SecurityCerts { get; set; } = [];

    public SampleVendorDocuments Documents { get; set; } = new();
}

public sealed class SampleVendorDocuments
{
    public bool ContractValid { get; set; }

    public bool PrivacyPolicyValid { get; set; }

    public bool PentestReportValid { get; set; }
}
