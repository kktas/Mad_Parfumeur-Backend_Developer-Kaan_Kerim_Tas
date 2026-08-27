namespace VendorRisk.Application.Dtos;

/// <summary>
/// Body of GET /api/vendor/{id}/risk. A superset of the example in case study section 8: the
/// riskScore, riskLevel and reason fields shown there, plus the breakdown behind them.
/// </summary>
public sealed class RiskAssessmentResponse
{
    public int VendorId { get; set; }

    public string VendorName { get; set; } = string.Empty;

    /// <summary>Always 0 in this build; see the Missing Code Notice in the README.</summary>
    public double RiskScore { get; set; }

    public string RiskLevel { get; set; } = string.Empty;

    public string Reason { get; set; } = string.Empty;

    public List<DimensionSummaryResponse> Dimensions { get; set; } = [];

    public List<RuleEvaluationResponse> TriggeredRules { get; set; } = [];

    public DateTime EvaluatedAtUtc { get; set; }
}
