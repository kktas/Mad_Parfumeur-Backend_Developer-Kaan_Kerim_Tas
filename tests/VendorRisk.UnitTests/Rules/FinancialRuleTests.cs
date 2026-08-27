using VendorRisk.Domain.Risk;
using VendorRisk.Domain.Rules;
using VendorRisk.UnitTests.TestSupport;

namespace VendorRisk.UnitTests.Rules;

public class FinancialRuleTests
{
    [Theory]
    [InlineData(0, true)]
    [InlineData(49, true)]
    [InlineData(50, false)]   // "< 50" is exclusive
    [InlineData(70, false)]
    public void LowFinancialHealth_fires_only_below_50(int financialHealth, bool shouldFire)
    {
        var vendor = new VendorBuilder().WithFinancialHealth(financialHealth).Build();

        var evaluation = new LowFinancialHealthRule().Evaluate(vendor);

        Assert.Equal(shouldFire, evaluation is not null);
        if (evaluation is not null)
        {
            Assert.Equal(RiskLevel.High, evaluation.Level);
            Assert.Equal(RiskCategory.Financial, evaluation.Category);
        }
    }

    [Theory]
    [InlineData(100, true)]
    [InlineData(81, true)]
    [InlineData(80, false)]   // "> 80" is exclusive
    [InlineData(70, false)]
    public void StrongFinancialHealth_fires_only_above_80(int financialHealth, bool shouldFire)
    {
        var vendor = new VendorBuilder().WithFinancialHealth(financialHealth).Build();

        var evaluation = new StrongFinancialHealthRule().Evaluate(vendor);

        Assert.Equal(shouldFire, evaluation is not null);
        if (evaluation is not null)
        {
            Assert.Equal(RiskLevel.Low, evaluation.Level);
        }
    }

    [Theory]
    [InlineData(50)]
    [InlineData(65)]
    [InlineData(80)]
    public void Financial_health_between_50_and_80_inclusive_fires_neither_rule(int financialHealth)
    {
        var vendor = new VendorBuilder().WithFinancialHealth(financialHealth).Build();

        Assert.Null(new LowFinancialHealthRule().Evaluate(vendor));
        Assert.Null(new StrongFinancialHealthRule().Evaluate(vendor));
    }
}
