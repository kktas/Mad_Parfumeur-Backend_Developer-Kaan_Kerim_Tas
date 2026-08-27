namespace VendorRisk.Application.Dtos;

/// <summary>Per-dimension roll-up. Always present for all three dimensions.</summary>
public sealed class DimensionSummaryResponse
{
    public string Category { get; set; } = string.Empty;

    public string RiskLevel { get; set; } = string.Empty;

    /// <summary>This dimension's risk in 0..1, before section 7's weighting is applied.</summary>
    public double Score { get; set; }

    /// <summary>The graded part of the score, and why it has that value.</summary>
    public CategoryBaselineResponse Baseline { get; set; } = new();

    public List<RuleEvaluationResponse> TriggeredRules { get; set; } = [];

    /// <summary>
    /// Risks the appendix A similarity matrix implies from the findings above, strongest first.
    /// The strongest one lifts the score; the rest are reported as part of the picture.
    /// </summary>
    public List<RelatedRiskResponse> RelatedRisks { get; set; } = [];
}

/// <summary>The graded component of a dimension's score.</summary>
public sealed class CategoryBaselineResponse
{
    public double Value { get; set; }

    /// <summary>e.g. "Financial health 75 of 100".</summary>
    public string Basis { get; set; } = string.Empty;
}

/// <summary>A risk implied by an observed finding through the similarity matrix.</summary>
public sealed class RelatedRiskResponse
{
    /// <summary>The implied risk item, e.g. "weakAccessControl".</summary>
    public string Risk { get; set; } = string.Empty;

    /// <summary>The matrix coefficient linking it to the finding.</summary>
    public double Similarity { get; set; }

    /// <summary>The finding's impact scaled by that coefficient.</summary>
    public double ImpliedImpact { get; set; }

    /// <summary>The rule whose finding implied it.</summary>
    public string SourceRuleId { get; set; } = string.Empty;
}
