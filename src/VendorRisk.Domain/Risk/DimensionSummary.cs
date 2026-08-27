namespace VendorRisk.Domain.Risk;

/// <summary>
/// Per-dimension roll-up of the rules that fired, and how they became a number.
/// </summary>
/// <param name="Category">The dimension being summarised.</param>
/// <param name="Level">Highest level among <paramref name="TriggeredRules"/>, or Low when none fired.</param>
/// <param name="TriggeredRules">Rules that fired within this dimension.</param>
/// <param name="Score">
/// The dimension's risk in 0..1: its findings and baseline combined, then lifted toward 1 by the
/// strongest risk the matrix implies. See <c>RuleBasedRiskScoringEngine</c>.
/// </param>
/// <param name="Baseline">The graded part of the score, with the reason it has that value.</param>
/// <param name="RelatedRisks">
/// Risks the matrix implies from the findings above, strongest first. Only the strongest lifts the
/// score; the rest are reported because they are part of the picture.
/// </param>
public sealed record DimensionSummary(
    RiskCategory Category,
    RiskLevel Level,
    IReadOnlyList<RuleEvaluation> TriggeredRules,
    double Score,
    CategoryBaseline Baseline,
    IReadOnlyList<RelatedRisk> RelatedRisks);
