namespace VendorRisk.Domain.Risk;

/// <summary>
/// The result of assessing one vendor (case study section 4).
/// </summary>
/// <param name="VendorId">Vendor the assessment belongs to.</param>
/// <param name="RiskScore">
/// Numeric score. Always 0 in this build: the weighted 0.4/0.3/0.3 formula from section 7 is not
/// implemented yet. See the Missing Code Notice in the README.
/// </param>
/// <param name="RiskLevel">Highest level among the triggered rules.</param>
/// <param name="Reason">Triggered clauses joined with " + ", most severe first.</param>
/// <param name="Dimensions">Per-dimension roll-up, always all three categories in enum order.</param>
/// <param name="TriggeredRules">Every rule that fired, most severe first.</param>
/// <param name="EvaluatedAtUtc">When the assessment was computed.</param>
public sealed record RiskAssessment(
    int VendorId,
    double RiskScore,
    RiskLevel RiskLevel,
    string Reason,
    IReadOnlyList<DimensionSummary> Dimensions,
    IReadOnlyList<RuleEvaluation> TriggeredRules,
    DateTime EvaluatedAtUtc);
