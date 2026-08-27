using VendorRisk.Domain.Risk;
using VendorRisk.Domain.Vendors;

namespace VendorRisk.Domain.Rules;

/// <summary>Section 5, financial factors: "FinancialHealth &lt; 50 -&gt; High risk".</summary>
public sealed class LowFinancialHealthRule : RiskRuleBase
{
    public override string RuleId => "LowFinancialHealth";

    public override RiskCategory Category => RiskCategory.Financial;

    public override RiskLevel Level => RiskLevel.High;

    protected override string Explanation => $"Financial health below {RiskThresholds.LowFinancialHealth}";

    public override string? MatrixNode => RiskFactorNodes.LowCashFlow;

    protected override bool IsTriggered(VendorProfile vendor) =>
        vendor.FinancialHealth < RiskThresholds.LowFinancialHealth;

    /// <summary>Graded across the remaining range: 49 sits just inside High, 0 reaches the cap.</summary>
    protected override double CalculateImpact(VendorProfile vendor)
    {
        var shortfall =
            (RiskThresholds.LowFinancialHealth - vendor.FinancialHealth) / (double)RiskThresholds.LowFinancialHealth;

        return RiskWeights.HighImpact + (RiskWeights.MaxEscalation * RiskWeights.Clamp(shortfall));
    }
}
