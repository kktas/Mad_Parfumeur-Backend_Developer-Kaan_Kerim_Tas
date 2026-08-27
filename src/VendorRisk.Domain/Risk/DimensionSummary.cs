namespace VendorRisk.Domain.Risk;

/// <summary>
/// Per-dimension roll-up of the rules that fired.
/// </summary>
/// <param name="Category">The dimension being summarised.</param>
/// <param name="Level">Highest level among <paramref name="TriggeredRules"/>, or Low when none fired.</param>
/// <param name="TriggeredRules">Rules that fired within this dimension.</param>
/// <param name="Score">
/// Reserved for the numeric engine and currently always 0. See <c>RuleBasedRiskScoringEngine</c>
/// and the Missing Code Notice in the README.
/// </param>
public sealed record DimensionSummary(
    RiskCategory Category,
    RiskLevel Level,
    IReadOnlyList<RuleEvaluation> TriggeredRules,
    double Score = 0d);
