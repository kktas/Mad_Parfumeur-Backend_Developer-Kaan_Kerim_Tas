namespace VendorRisk.Application.Dtos;

/// <summary>A vendor as returned by the API, matching VendorProfile in case study section 4.</summary>
public sealed class VendorResponse
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public int FinancialHealth { get; set; }

    public decimal SlaUptime { get; set; }

    public int MajorIncidents { get; set; }

    public List<string> SecurityCerts { get; set; } = [];

    public VendorDocumentsDto Documents { get; set; } = new();
}
