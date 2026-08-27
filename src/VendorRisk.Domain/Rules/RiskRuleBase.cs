using VendorRisk.Domain.Risk;
using VendorRisk.Domain.Vendors;

namespace VendorRisk.Domain.Rules;

/// <summary>
/// Boilerplate shared by the section 5 rules: a rule declares its identity, its severity, its
/// explanation and a single predicate, and this base turns that into a <see cref="RuleEvaluation"/>.
/// </summary>
public abstract class RiskRuleBase : IRiskRule
{
    public abstract string RuleId { get; }

    public abstract RiskCategory Category { get; }

    public abstract RiskLevel Level { get; }

    /// <summary>Clause used in the assessment reason, without the level suffix.</summary>
    protected abstract string Explanation { get; }

    /// <summary>Whether the condition described in section 5 holds for this vendor.</summary>
    protected abstract bool IsTriggered(VendorProfile vendor);

    public RuleEvaluation? Evaluate(VendorProfile vendor)
    {
        ArgumentNullException.ThrowIfNull(vendor);

        return IsTriggered(vendor)
            ? new RuleEvaluation(RuleId, Category, Level, Explanation)
            : null;
    }
}
