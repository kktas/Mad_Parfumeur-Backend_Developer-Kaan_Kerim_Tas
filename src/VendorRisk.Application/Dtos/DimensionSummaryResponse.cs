namespace VendorRisk.Application.Dtos;

/// <summary>Per-dimension roll-up. Always present for all three dimensions.</summary>
public sealed class DimensionSummaryResponse
{
    public string Category { get; set; } = string.Empty;

    public string RiskLevel { get; set; } = string.Empty;

    /// <summary>Always 0 in this build; see the Missing Code Notice in the README.</summary>
    public double Score { get; set; }

    public List<RuleEvaluationResponse> TriggeredRules { get; set; } = [];
}
