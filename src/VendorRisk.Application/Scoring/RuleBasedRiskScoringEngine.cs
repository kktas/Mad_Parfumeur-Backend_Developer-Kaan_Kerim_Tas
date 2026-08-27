using Microsoft.Extensions.Logging;
using VendorRisk.Application.Abstractions;
using VendorRisk.Domain.Risk;
using VendorRisk.Domain.Vendors;

namespace VendorRisk.Application.Scoring;

/// <summary>
/// Evaluates every registered <see cref="IRiskRule"/> against a vendor and rolls the results up
/// into an explainable assessment:
///
///   - the reason lists each fired rule as "Explanation (Level)", most severe first;
///   - each dimension scores its findings, its graded baseline and the risks the matrix implies;
///   - the score weights the dimensions as section 7 of the case study does, 0.4 / 0.3 / 0.3;
///   - the level is the more severe of the highest triggered rule and the score's own band.
///
/// Rules arrive by constructor injection, so introducing a rule requires no change here. Every
/// number the arithmetic uses lives in <see cref="RiskWeights"/>, and the assumptions behind them
/// are set out in the README's scoring section.
/// </summary>
public sealed class RuleBasedRiskScoringEngine : IRiskScoringEngine
{
    /// <summary>Reason returned when no rule fires at all.</summary>
    public const string NoFindingsReason = "No significant risk factors identified (Low)";

    private const string ReasonSeparator = " + ";

    private readonly IReadOnlyList<IRiskRule> _rules;
    private readonly IRiskFactorMatrix _matrix;
    private readonly ILogger<RuleBasedRiskScoringEngine> _logger;
    private readonly TimeProvider _timeProvider;

    public RuleBasedRiskScoringEngine(
        IEnumerable<IRiskRule> rules,
        IRiskFactorMatrix matrix,
        ILogger<RuleBasedRiskScoringEngine> logger,
        TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(rules);

        _rules = rules.ToList();
        _matrix = matrix ?? throw new ArgumentNullException(nameof(matrix));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public RiskAssessment Evaluate(VendorProfile vendor)
    {
        ArgumentNullException.ThrowIfNull(vendor);

        // OrderByDescending is a stable sort, so rules of equal severity keep registration order
        // and the reason string is deterministic.
        var triggered = _rules
            .Select(rule => rule.Evaluate(vendor))
            .OfType<RuleEvaluation>()
            .OrderByDescending(evaluation => evaluation.Level)
            .ToList();

        var reason = triggered.Count == 0
            ? NoFindingsReason
            : string.Join(ReasonSeparator, triggered.Select(evaluation => evaluation.ToReasonClause()));

        var dimensions = BuildDimensions(vendor, triggered);

        // Section 7's weighted sum, taken over the rounded dimension scores so the arithmetic shown
        // in the payload is the arithmetic that produced the total.
        var riskScore = RiskWeights.Round(
            dimensions.Sum(dimension => RiskWeights.WeightOf(dimension.Category) * dimension.Score));

        var riskLevel = OverallLevel(triggered, riskScore);

        _logger.LogInformation(
            "Assessed vendor {VendorId} as {RiskLevel} scoring {RiskScore} from {TriggeredRuleCount} triggered rule(s): {TriggeredRuleIds}",
            vendor.Id,
            riskLevel,
            riskScore,
            triggered.Count,
            triggered.Select(evaluation => evaluation.RuleId));

        return new RiskAssessment(
            VendorId: vendor.Id,
            RiskScore: riskScore,
            RiskLevel: riskLevel,
            Reason: reason,
            Dimensions: dimensions,
            TriggeredRules: triggered,
            EvaluatedAtUtc: _timeProvider.GetUtcNow().UtcDateTime);
    }

    /// <summary>
    /// The more severe of the two readings. The score may raise the level - several Medium findings
    /// together are worse than any one of them alone - but it must never lower it: a failed
    /// penetration test scores only 0.30 overall on an otherwise clean vendor, and that cannot be
    /// allowed to read as anything but Critical.
    /// </summary>
    private static RiskLevel OverallLevel(IReadOnlyList<RuleEvaluation> triggered, double riskScore)
    {
        var fromRules = triggered.Count == 0 ? RiskLevel.Low : triggered.Max(evaluation => evaluation.Level);
        var fromScore = RiskWeights.LevelFor(riskScore);

        return fromRules >= fromScore ? fromRules : fromScore;
    }

    /// <summary>
    /// Produces one summary per category, in enum order, including categories where nothing fired
    /// so that consumers such as the comparison dashboard always see the same shape.
    /// </summary>
    private IReadOnlyList<DimensionSummary> BuildDimensions(
        VendorProfile vendor,
        IReadOnlyList<RuleEvaluation> triggered) =>
        Enum.GetValues<RiskCategory>()
            .Select(category => BuildDimension(vendor, category, triggered))
            .ToList();

    private DimensionSummary BuildDimension(
        VendorProfile vendor,
        RiskCategory category,
        IReadOnlyList<RuleEvaluation> triggered)
    {
        var findings = triggered.Where(evaluation => evaluation.Category == category).ToList();
        var baseline = CategoryBaselines.For(category, vendor);
        var relatedRisks = ImpliedRisks(findings);

        // Findings and the baseline combine as independent contributors: each raises the score by a
        // share of what is left, so more findings always mean more risk and the total can never
        // pass 1. A Critical finding contributes 1 and saturates the dimension outright.
        var observed = CombineIndependent(findings.Select(finding => finding.Impact).Append(baseline.Value));

        // The strongest implied risk closes part of the remaining distance to 1. It is inferred
        // rather than observed, hence the damping; a saturated dimension has no room left for it.
        var strongestImplied = relatedRisks.Count == 0 ? 0d : relatedRisks[0].ImpliedImpact;
        var score = observed + ((1d - observed) * RiskWeights.ImpliedRiskDamping * strongestImplied);

        return new DimensionSummary(
            Category: category,
            Level: findings.Count == 0 ? RiskLevel.Low : findings.Max(finding => finding.Level),
            TriggeredRules: findings,
            Score: RiskWeights.Round(score),
            Baseline: baseline with { Value = RiskWeights.Round(baseline.Value) },
            RelatedRisks: relatedRisks);
    }

    /// <summary>
    /// How likely at least one of the contributions applies, treating them as independent:
    /// 1 - (1 - a)(1 - b) and so on. Order-independent, monotonic, and bounded by 1.
    /// </summary>
    private static double CombineIndependent(IEnumerable<double> impacts)
    {
        var remaining = 1d;

        foreach (var impact in impacts.Where(impact => impact > 0d))
        {
            remaining *= 1d - RiskWeights.Clamp(impact);
        }

        return 1d - remaining;
    }

    /// <summary>
    /// Reads appendix A's similarity matrix the way section 2.3 asks: a finding implies the risks
    /// that tend to come with it, each scaled by how strongly the matrix associates the two. Where
    /// two findings imply the same risk the stronger one wins, and a risk already observed in this
    /// dimension is skipped rather than counted twice.
    /// </summary>
    private IReadOnlyList<RelatedRisk> ImpliedRisks(IReadOnlyList<RuleEvaluation> findings)
    {
        var observedNodes = findings
            .Select(finding => finding.MatrixNode)
            .OfType<string>()
            .ToHashSet(StringComparer.Ordinal);

        var strongestByNode = new Dictionary<string, RelatedRisk>(StringComparer.Ordinal);

        foreach (var finding in findings.Where(finding => finding.MatrixNode is not null && finding.Impact > 0d))
        {
            foreach (var neighbour in _matrix.Related(finding.MatrixNode!))
            {
                if (observedNodes.Contains(neighbour.Node))
                {
                    continue;
                }

                var implied = new RelatedRisk(
                    neighbour.Node,
                    neighbour.Similarity,
                    RiskWeights.Round(finding.Impact * neighbour.Similarity),
                    finding.RuleId);

                if (!strongestByNode.TryGetValue(neighbour.Node, out var existing)
                    || implied.ImpliedImpact > existing.ImpliedImpact)
                {
                    strongestByNode[neighbour.Node] = implied;
                }
            }
        }

        return [.. strongestByNode.Values
            .OrderByDescending(risk => risk.ImpliedImpact)
            .ThenBy(risk => risk.Node, StringComparer.Ordinal)];
    }
}
