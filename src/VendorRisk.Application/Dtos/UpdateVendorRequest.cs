using System.ComponentModel.DataAnnotations;

namespace VendorRisk.Application.Dtos;

/// <summary>Body of PUT /api/vendor/{id}. A full replacement of the vendor's assessment inputs.</summary>
public sealed class UpdateVendorRequest
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

    public VendorDocumentsDto Documents { get; set; } = new();
}
