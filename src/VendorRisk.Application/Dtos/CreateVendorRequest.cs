using System.ComponentModel.DataAnnotations;

namespace VendorRisk.Application.Dtos;

/// <summary>Body of POST /api/vendor, matching the example in case study section 8.</summary>
public sealed class CreateVendorRequest
{
    [Required(AllowEmptyStrings = false, ErrorMessage = "Vendor name is required.")]
    [StringLength(200, MinimumLength = 2)]
    public string Name { get; set; } = string.Empty;

    [Range(0, 100, ErrorMessage = "financialHealth must be between 0 and 100.")]
    public int FinancialHealth { get; set; }

    [Range(0, 100, ErrorMessage = "slaUptime must be between 0 and 100.")]
    public decimal SlaUptime { get; set; }

    [Range(0, int.MaxValue, ErrorMessage = "majorIncidents cannot be negative.")]
    public int MajorIncidents { get; set; }

    public List<string> SecurityCerts { get; set; } = [];

    /// <summary>Optional; omitted flags default to false, i.e. the document is treated as invalid.</summary>
    public VendorDocumentsDto Documents { get; set; } = new();
}
