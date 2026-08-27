using VendorRisk.Domain.Risk;
using VendorRisk.Domain.Vendors;

namespace VendorRisk.Domain.Rules;

/// <summary>Section 5, operational factors: "SLA uptime &lt; 95% -&gt; High operational risk".</summary>
public sealed class SlaBelowThresholdRule : RiskRuleBase
{
    public override string RuleId => "SlaBelowThreshold";

    public override RiskCategory Category => RiskCategory.Operational;

    public override RiskLevel Level => RiskLevel.High;

    protected override string Explanation => $"SLA below {RiskThresholds.MinimumSlaUptime:0.##}%";

    protected override bool IsTriggered(VendorProfile vendor) =>
        vendor.SlaUptime < RiskThresholds.MinimumSlaUptime;
}
