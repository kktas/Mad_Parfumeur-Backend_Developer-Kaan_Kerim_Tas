namespace VendorRisk.Application.Dtos;

/// <summary>Body of GET /api/vendor/compare, backing the comparison dashboard.</summary>
public sealed class VendorComparisonResponse
{
    public List<VendorComparisonItem> Vendors { get; set; } = [];

    /// <summary>Ids that were requested but do not exist, so the caller can tell them apart from ignored input.</summary>
    public List<int> NotFoundIds { get; set; } = [];
}

/// <summary>One column of the comparison: the vendor's inputs alongside its assessment.</summary>
public sealed class VendorComparisonItem
{
    public VendorResponse Vendor { get; set; } = new();

    public RiskAssessmentResponse Assessment { get; set; } = new();
}
