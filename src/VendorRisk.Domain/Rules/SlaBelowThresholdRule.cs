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

    public override string? MatrixNode => RiskFactorNodes.SlaDrop;

    protected override bool IsTriggered(VendorProfile vendor) =>
        vendor.SlaUptime < RiskThresholds.MinimumSlaUptime;

    /// <summary>
    /// Graded by how far the SLA falls short: a hair under the target sits just inside High, and a
    /// full escalation window below it reaches the cap.
    /// </summary>
    protected override double CalculateImpact(VendorProfile vendor)
    {
        var shortfall = (RiskThresholds.MinimumSlaUptime - vendor.SlaUptime) / RiskWeights.SlaEscalationWindow;

        return RiskWeights.HighImpact + (RiskWeights.MaxEscalation * RiskWeights.Clamp((double)shortfall));
    }
}
