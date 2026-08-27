using VendorRisk.Domain.Risk;
using VendorRisk.Domain.Vendors;

namespace VendorRisk.Domain.Rules;

/// <summary>
/// Section 5, financial factors: "FinancialHealth &gt; 80 -&gt; Low risk". This is a favourable
/// finding: it appears in the reason for transparency but, being Low, never raises the overall level.
/// Note that a score between 50 and 80 inclusive triggers neither financial rule.
/// </summary>
public sealed class StrongFinancialHealthRule : RiskRuleBase
{
    public override string RuleId => "StrongFinancialHealth";

    public override RiskCategory Category => RiskCategory.Financial;

    public override RiskLevel Level => RiskLevel.Low;

    protected override string Explanation => $"Strong financial health above {RiskThresholds.StrongFinancialHealth}";

    protected override bool IsTriggered(VendorProfile vendor) =>
        vendor.FinancialHealth > RiskThresholds.StrongFinancialHealth;
}
