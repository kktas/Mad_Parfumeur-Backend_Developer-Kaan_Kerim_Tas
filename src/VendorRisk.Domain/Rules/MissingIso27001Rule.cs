using VendorRisk.Domain.Risk;
using VendorRisk.Domain.Vendors;

namespace VendorRisk.Domain.Rules;

/// <summary>Section 5, security and compliance: "Missing ISO27001 -&gt; High security risk".</summary>
public sealed class MissingIso27001Rule : RiskRuleBase
{
    public override string RuleId => "MissingIso27001";

    public override RiskCategory Category => RiskCategory.SecurityCompliance;

    public override RiskLevel Level => RiskLevel.High;

    protected override string Explanation => $"Missing {RiskThresholds.RequiredCertification}";

    protected override bool IsTriggered(VendorProfile vendor) =>
        !vendor.HasCertification(RiskThresholds.RequiredCertification);
}
