using Microsoft.Extensions.Logging;
using VendorRisk.Application.Abstractions;
using VendorRisk.Domain.Risk;
using VendorRisk.Domain.Vendors;

namespace VendorRisk.Application.Scoring;

/// <summary>
/// Evaluates every registered <see cref="IRiskRule"/> against a vendor and rolls the results up
/// into an explainable assessment:
///
///   - the overall level is the highest level among the rules that fired;
///   - the reason lists each fired rule as "Explanation (Level)", most severe first;
///   - dimensions carry the same roll-up per category.
///
/// Rules arrive by constructor injection, so introducing a rule requires no change here.
/// </summary>
public sealed class RuleBasedRiskScoringEngine : IRiskScoringEngine
{
    /// <summary>Reason returned when no rule fires at all.</summary>
    public const string NoFindingsReason = "No significant risk factors identified (Low)";

    private const string ReasonSeparator = " + ";

    private readonly IReadOnlyList<IRiskRule> _rules;
    private readonly ILogger<RuleBasedRiskScoringEngine> _logger;
    private readonly TimeProvider _timeProvider;

    public RuleBasedRiskScoringEngine(
        IEnumerable<IRiskRule> rules,
        ILogger<RuleBasedRiskScoringEngine> logger,
        TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(rules);

        _rules = rules.ToList();
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

        var riskLevel = triggered.Count == 0
            ? RiskLevel.Low
            : triggered.Max(evaluation => evaluation.Level);

        var reason = triggered.Count == 0
            ? NoFindingsReason
            : string.Join(ReasonSeparator, triggered.Select(evaluation => evaluation.ToReasonClause()));

        var dimensions = BuildDimensions(triggered);

        _logger.LogInformation(
            "Assessed vendor {VendorId} as {RiskLevel} from {TriggeredRuleCount} triggered rule(s): {TriggeredRuleIds}",
            vendor.Id,
            riskLevel,
            triggered.Count,
            triggered.Select(evaluation => evaluation.RuleId));

        return new RiskAssessment(
            VendorId: vendor.Id,
            // TODO: implement the weighted score from case study section 7 here, i.e.
            //       (FinancialRisk * 0.4) + (OperationalRisk * 0.3) + (SecurityComplianceRisk * 0.3)
            //       over per-rule impact weights, and derive RiskLevel from score thresholds
            //       instead of from the maximum triggered level. Reported as 0 until then.
            RiskScore: 0d,
            RiskLevel: riskLevel,
            Reason: reason,
            Dimensions: dimensions,
            TriggeredRules: triggered,
            EvaluatedAtUtc: _timeProvider.GetUtcNow().UtcDateTime);
    }

    /// <summary>
    /// Produces one summary per category, in enum order, including categories where nothing fired
    /// so that consumers such as the comparison dashboard always see the same shape.
    /// </summary>
    private static IReadOnlyList<DimensionSummary> BuildDimensions(IReadOnlyList<RuleEvaluation> triggered) =>
        Enum.GetValues<RiskCategory>()
            .Select(category =>
            {
                var forCategory = triggered.Where(evaluation => evaluation.Category == category).ToList();

                return new DimensionSummary(
                    Category: category,
                    Level: forCategory.Count == 0 ? RiskLevel.Low : forCategory.Max(evaluation => evaluation.Level),
                    TriggeredRules: forCategory,
                    // TODO: per-dimension numeric score, alongside the weighted score above.
                    Score: 0d);
            })
            .ToList();
}
