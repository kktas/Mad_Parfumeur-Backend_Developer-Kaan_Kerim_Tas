using VendorRisk.Domain.Risk;
using VendorRisk.Domain.Vendors;

namespace VendorRisk.Domain.Rules;

/// <summary>Section 5, security and compliance: "Privacy Policy expired -&gt; Moderate compliance risk".</summary>
public sealed class PrivacyPolicyExpiredRule : RiskRuleBase
{
    public override string RuleId => "PrivacyPolicyExpired";

    public override RiskCategory Category => RiskCategory.SecurityCompliance;

    public override RiskLevel Level => RiskLevel.Medium;

    protected override string Explanation => "Privacy policy expired";

    public override string? MatrixNode => RiskFactorNodes.ExpiredPrivacyPolicy;

    protected override bool IsTriggered(VendorProfile vendor) => !vendor.Documents.PrivacyPolicyValid;
}
