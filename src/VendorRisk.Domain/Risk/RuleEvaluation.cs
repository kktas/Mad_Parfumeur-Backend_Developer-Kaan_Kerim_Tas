namespace VendorRisk.Domain.Risk;

/// <summary>
/// The outcome of a single rule that fired for a vendor.
/// </summary>
/// <param name="RuleId">Stable identifier of the rule, e.g. "MissingIso27001".</param>
/// <param name="Category">Dimension the rule belongs to.</param>
/// <param name="Level">Severity contributed by this rule.</param>
/// <param name="Explanation">Human-readable clause, e.g. "Missing ISO27001".</param>
public sealed record RuleEvaluation(
    string RuleId,
    RiskCategory Category,
    RiskLevel Level,
    string Explanation)
{
    /// <summary>Renders the clause as it appears in an assessment reason, e.g. "Missing ISO27001 (High)".</summary>
    public string ToReasonClause() => $"{Explanation} ({Level})";
}
