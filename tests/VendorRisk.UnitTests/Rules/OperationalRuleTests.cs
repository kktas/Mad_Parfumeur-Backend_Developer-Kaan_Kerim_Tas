using VendorRisk.Domain.Risk;
using VendorRisk.Domain.Rules;
using VendorRisk.UnitTests.TestSupport;

namespace VendorRisk.UnitTests.Rules;

public class OperationalRuleTests
{
    [Theory]
    [InlineData(86, true)]
    [InlineData(94.99, true)]
    [InlineData(95, false)]   // "< 95%" is exclusive
    [InlineData(99, false)]
    public void SlaBelowThreshold_fires_only_below_95(decimal slaUptime, bool shouldFire)
    {
        var vendor = new VendorBuilder().WithSlaUptime(slaUptime).Build();

        var evaluation = new SlaBelowThresholdRule().Evaluate(vendor);

        Assert.Equal(shouldFire, evaluation is not null);
        if (evaluation is not null)
        {
            Assert.Equal(RiskLevel.High, evaluation.Level);
            Assert.Equal(RiskCategory.Operational, evaluation.Category);
        }
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(2, false)]    // "> 2" is exclusive
    [InlineData(3, true)]
    [InlineData(4, true)]
    public void MajorIncidents_fires_only_above_two(int majorIncidents, bool shouldFire)
    {
        var vendor = new VendorBuilder().WithMajorIncidents(majorIncidents).Build();

        var evaluation = new MajorIncidentsRule().Evaluate(vendor);

        Assert.Equal(shouldFire, evaluation is not null);
        if (evaluation is not null)
        {
            Assert.Equal(RiskLevel.High, evaluation.Level);
        }
    }

    /// <summary>
    /// Pins the documented gap: the case study defines this rule but the dataset carries no
    /// ticket-resolution field, so it never fires. Replace this test when the field is added.
    /// </summary>
    [Fact]
    public void SlowTicketResolution_never_fires_because_no_ticket_data_exists()
    {
        var rule = new SlowTicketResolutionRule();

        Assert.Null(rule.Evaluate(VendorBuilder.Clean()));
        Assert.Null(rule.Evaluate(new VendorBuilder().WithMajorIncidents(10).WithSlaUptime(1).Build()));
        Assert.Equal(RiskLevel.Medium, rule.Level);
    }
}
