using VendorRisk.Domain.Risk;
using VendorRisk.Domain.Vendors;

namespace VendorRisk.Domain.Rules;

/// <summary>Section 5, operational factors: "Major incidents &gt; 2 (last 12 months) -&gt; High risk".</summary>
public sealed class MajorIncidentsRule : RiskRuleBase
{
    public override string RuleId => "MajorIncidents";

    public override RiskCategory Category => RiskCategory.Operational;

    public override RiskLevel Level => RiskLevel.High;

    protected override string Explanation =>
        $"More than {RiskThresholds.MaxAcceptableMajorIncidents} major incidents in the last 12 months";

    public override string? MatrixNode => RiskFactorNodes.MajorIncident;

    protected override bool IsTriggered(VendorProfile vendor) =>
        vendor.MajorIncidents > RiskThresholds.MaxAcceptableMajorIncidents;

    /// <summary>Graded by how far past the threshold the count runs, so 3 and 30 do not score alike.</summary>
    protected override double CalculateImpact(VendorProfile vendor)
    {
        var excess = (vendor.MajorIncidents - RiskThresholds.MaxAcceptableMajorIncidents)
            / (double)RiskWeights.IncidentEscalationWindow;

        return RiskWeights.HighImpact + (RiskWeights.MaxEscalation * RiskWeights.Clamp(excess));
    }
}
