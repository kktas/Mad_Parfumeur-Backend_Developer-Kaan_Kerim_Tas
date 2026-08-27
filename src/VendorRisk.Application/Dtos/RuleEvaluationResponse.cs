namespace VendorRisk.Application.Dtos;

/// <summary>One rule that fired, exposed so clients can render the finding list themselves.</summary>
public sealed class RuleEvaluationResponse
{
    public string RuleId { get; set; } = string.Empty;

    public string Category { get; set; } = string.Empty;

    public string RiskLevel { get; set; } = string.Empty;

    public string Explanation { get; set; } = string.Empty;
}
