using VendorRisk.Domain.Vendors;

namespace VendorRisk.Domain.Risk;

/// <summary>
/// A single rule from section 5 of the case study. Implementations are registered in DI and
/// discovered by the scoring engine, so adding a rule needs no change to the engine.
/// </summary>
public interface IRiskRule
{
    /// <summary>Stable identifier, used in logs and in the assessment payload.</summary>
    string RuleId { get; }

    /// <summary>Dimension this rule contributes to.</summary>
    RiskCategory Category { get; }

    /// <summary>Severity this rule contributes when it fires.</summary>
    RiskLevel Level { get; }

    /// <summary>Evaluates the vendor, returning <c>null</c> when the rule does not fire.</summary>
    RuleEvaluation? Evaluate(VendorProfile vendor);
}
