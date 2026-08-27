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

    /// <summary>Matrix entry this rule observes. Rules with no matching risk item leave it null.</summary>
    public virtual string? MatrixNode => null;

    /// <summary>Clause used in the assessment reason, without the level suffix.</summary>
    protected abstract string Explanation { get; }

    /// <summary>Whether the condition described in section 5 holds for this vendor.</summary>
    protected abstract bool IsTriggered(VendorProfile vendor);

    /// <summary>
    /// What this finding contributes to its category's score. The default is the impact of the
    /// rule's severity; rules over a continuous input override this to grade themselves within
    /// their band, so that a vendor barely past a threshold does not score like one far past it.
    /// </summary>
    protected virtual double CalculateImpact(VendorProfile vendor) => RiskWeights.ImpactOf(Level);

    public RuleEvaluation? Evaluate(VendorProfile vendor)
    {
        ArgumentNullException.ThrowIfNull(vendor);

        return IsTriggered(vendor)
            ? new RuleEvaluation(RuleId, Category, Level, Explanation, CalculateImpact(vendor), MatrixNode)
            : null;
    }
}
