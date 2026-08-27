using VendorRisk.Application.Dtos;
using VendorRisk.Domain.Risk;
using VendorRisk.Domain.Vendors;

namespace VendorRisk.Application.Mapping;

/// <summary>
/// Translation between domain types and the API contract. Hand-written rather than convention-based
/// so the JSON shape stays pinned to the case study examples.
/// </summary>
public static class VendorMapper
{
    public static VendorResponse ToResponse(this VendorProfile vendor) => new()
    {
        Id = vendor.Id,
        Name = vendor.Name,
        FinancialHealth = vendor.FinancialHealth,
        SlaUptime = vendor.SlaUptime,
        MajorIncidents = vendor.MajorIncidents,
        SecurityCerts = [.. vendor.SecurityCerts],
        Documents = new VendorDocumentsDto
        {
            ContractValid = vendor.Documents.ContractValid,
            PrivacyPolicyValid = vendor.Documents.PrivacyPolicyValid,
            PentestReportValid = vendor.Documents.PentestReportValid
        }
    };

    /// <summary>
    /// Maps the request onto a new vendor. Certificates are left empty: the service resolves the
    /// requested codes against the catalogue and sets them.
    /// </summary>
    public static VendorProfile ToDomain(this CreateVendorRequest request, DateTime nowUtc) => new()
    {
        Name = request.Name.Trim(),
        FinancialHealth = request.FinancialHealth,
        SlaUptime = request.SlaUptime,
        MajorIncidents = request.MajorIncidents,
        Documents = ToDomain(request.Documents),
        CreatedAtUtc = nowUtc,
        UpdatedAtUtc = nowUtc
    };

    /// <summary>
    /// Applies a full replacement update onto an existing vendor. Certificates are not touched
    /// here: they are catalogue rows the service resolves and hands to
    /// <see cref="VendorProfile.SetCertificates"/>.
    /// </summary>
    public static void ApplyTo(this UpdateVendorRequest request, VendorProfile vendor, DateTime nowUtc)
    {
        vendor.Name = request.Name.Trim();
        vendor.FinancialHealth = request.FinancialHealth;
        vendor.SlaUptime = request.SlaUptime;
        vendor.MajorIncidents = request.MajorIncidents;
        vendor.Documents = ToDomain(request.Documents);
        vendor.UpdatedAtUtc = nowUtc;
    }

    public static RiskAssessmentResponse ToResponse(this RiskAssessment assessment, string vendorName) => new()
    {
        VendorId = assessment.VendorId,
        VendorName = vendorName,
        RiskScore = assessment.RiskScore,
        RiskLevel = assessment.RiskLevel.ToString(),
        Reason = assessment.Reason,
        Dimensions = [.. assessment.Dimensions.Select(ToResponse)],
        TriggeredRules = [.. assessment.TriggeredRules.Select(ToResponse)],
        EvaluatedAtUtc = assessment.EvaluatedAtUtc
    };

    private static DimensionSummaryResponse ToResponse(DimensionSummary dimension) => new()
    {
        Category = dimension.Category.ToString(),
        RiskLevel = dimension.Level.ToString(),
        Score = dimension.Score,
        TriggeredRules = [.. dimension.TriggeredRules.Select(ToResponse)]
    };

    private static RuleEvaluationResponse ToResponse(RuleEvaluation evaluation) => new()
    {
        RuleId = evaluation.RuleId,
        Category = evaluation.Category.ToString(),
        RiskLevel = evaluation.Level.ToString(),
        Explanation = evaluation.Explanation
    };

    private static VendorDocuments ToDomain(VendorDocumentsDto documents) => new()
    {
        ContractValid = documents.ContractValid,
        PrivacyPolicyValid = documents.PrivacyPolicyValid,
        PentestReportValid = documents.PentestReportValid
    };
}
