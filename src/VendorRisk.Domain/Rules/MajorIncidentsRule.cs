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

    protected override bool IsTriggered(VendorProfile vendor) =>
        vendor.MajorIncidents > RiskThresholds.MaxAcceptableMajorIncidents;
}
