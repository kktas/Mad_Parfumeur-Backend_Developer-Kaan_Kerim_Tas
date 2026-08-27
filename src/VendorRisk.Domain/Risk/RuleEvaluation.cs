namespace VendorRisk.Domain.Risk;

/// <summary>
/// The outcome of a single rule that fired for a vendor.
/// </summary>
/// <param name="RuleId">Stable identifier of the rule, e.g. "MissingIso27001".</param>
/// <param name="Category">Dimension the rule belongs to.</param>
/// <param name="Level">Severity contributed by this rule.</param>
/// <param name="Explanation">Human-readable clause, e.g. "Missing ISO27001".</param>
/// <param name="Impact">
/// What this finding contributes to its category's score, in 0..1. Defaults to the severity's
/// impact; rules over a continuous input grade it within their band. A favourable finding is 0.
/// </param>
/// <param name="MatrixNode">
/// The RiskFactorMatrix.json entry this finding observes, or <c>null</c> when it maps to none.
/// </param>
public sealed record RuleEvaluation(
    string RuleId,
    RiskCategory Category,
    RiskLevel Level,
    string Explanation,
    double Impact = 0d,
    string? MatrixNode = null)
{
    /// <summary>Renders the clause as it appears in an assessment reason, e.g. "Missing ISO27001 (High)".</summary>
    public string ToReasonClause() => $"{Explanation} ({Level})";
}
