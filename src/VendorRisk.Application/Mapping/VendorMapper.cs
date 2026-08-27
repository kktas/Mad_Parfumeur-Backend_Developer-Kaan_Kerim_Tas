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

    public static VendorProfile ToDomain(this CreateVendorRequest request, DateTime nowUtc) => new()
    {
        Name = request.Name.Trim(),
        FinancialHealth = request.FinancialHealth,
        SlaUptime = request.SlaUptime,
        MajorIncidents = request.MajorIncidents,
        SecurityCerts = NormaliseCerts(request.SecurityCerts),
        Documents = ToDomain(request.Documents),
        CreatedAtUtc = nowUtc,
        UpdatedAtUtc = nowUtc
    };

    /// <summary>Applies a full replacement update onto an existing vendor.</summary>
    public static void ApplyTo(this UpdateVendorRequest request, VendorProfile vendor, DateTime nowUtc)
    {
        vendor.Name = request.Name.Trim();
        vendor.FinancialHealth = request.FinancialHealth;
        vendor.SlaUptime = request.SlaUptime;
        vendor.MajorIncidents = request.MajorIncidents;
        vendor.SecurityCerts = NormaliseCerts(request.SecurityCerts);
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

    /// <summary>Trims and drops blanks so certificate matching is not defeated by stray whitespace.</summary>
    private static List<string> NormaliseCerts(IEnumerable<string>? certs) =>
        [.. (certs ?? []).Where(cert => !string.IsNullOrWhiteSpace(cert)).Select(cert => cert.Trim())];
}
