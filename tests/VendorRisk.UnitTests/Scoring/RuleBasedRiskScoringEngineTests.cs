using VendorRisk.Application.Scoring;
using VendorRisk.Domain.Risk;
using VendorRisk.Domain.Rules;
using VendorRisk.UnitTests.TestSupport;

namespace VendorRisk.UnitTests.Scoring;

public class RuleBasedRiskScoringEngineTests
{
    [Fact]
    public void Overall_level_is_the_highest_triggered_level()
    {
        // Medium (privacy policy) and High (missing ISO27001) fire; High wins.
        var vendor = new VendorBuilder()
            .WithCerts()
            .WithDocuments(privacyPolicyValid: false)
            .Build();

        var assessment = EngineFactory.Create().Evaluate(vendor);

        Assert.Equal(RiskLevel.High, assessment.RiskLevel);
    }

    [Fact]
    public void A_single_critical_rule_outranks_several_high_rules()
    {
        var vendor = new VendorBuilder()
            .WithFinancialHealth(40)
            .WithSlaUptime(80)
            .WithMajorIncidents(5)
            .WithCerts()
            .WithDocuments(privacyPolicyValid: false, pentestReportValid: false)
            .Build();

        var assessment = EngineFactory.Create().Evaluate(vendor);

        Assert.Equal(RiskLevel.Critical, assessment.RiskLevel);
        Assert.StartsWith("Failed penetration test (Critical)", assessment.Reason);
    }

    [Fact]
    public void Reason_lists_every_finding_most_severe_first()
    {
        var vendor = new VendorBuilder()
            .WithFinancialHealth(90)             // Low  - strong financial health
            .WithSlaUptime(93)                   // High - SLA below 95%
            .WithCerts()                         // High - missing ISO27001
            .WithDocuments(privacyPolicyValid: false)  // Medium - privacy policy expired
            .Build();

        var assessment = EngineFactory.Create().Evaluate(vendor);

        Assert.Equal(
            "SLA below 95% (High) + Missing ISO27001 (High) + Privacy policy expired (Medium) + " +
            "Strong financial health above 80 (Low)",
            assessment.Reason);
    }

    [Fact]
    public void Equal_severity_findings_keep_rule_registration_order()
    {
        // Both are High. Section 5 lists the SLA rule before the ISO27001 rule, and registration
        // order is the documented tie-break, so the SLA clause must come first.
        var vendor = new VendorBuilder().WithSlaUptime(90).WithCerts().Build();

        var assessment = EngineFactory.Create().Evaluate(vendor);

        Assert.Equal("SLA below 95% (High) + Missing ISO27001 (High)", assessment.Reason);
    }

    [Fact]
    public void A_vendor_that_trips_no_rule_is_low_with_an_explicit_reason()
    {
        // Financial health inside 50..80 so neither financial rule fires either.
        var assessment = EngineFactory.Create().Evaluate(VendorBuilder.Clean());

        Assert.Equal(RiskLevel.Low, assessment.RiskLevel);
        Assert.Empty(assessment.TriggeredRules);
        Assert.Equal(RuleBasedRiskScoringEngine.NoFindingsReason, assessment.Reason);
    }

    [Fact]
    public void Dimensions_always_cover_all_three_categories_in_enum_order()
    {
        var assessment = EngineFactory.Create().Evaluate(VendorBuilder.Clean());

        Assert.Equal(
            [RiskCategory.Financial, RiskCategory.Operational, RiskCategory.SecurityCompliance],
            assessment.Dimensions.Select(dimension => dimension.Category));
        Assert.All(assessment.Dimensions, dimension => Assert.Equal(RiskLevel.Low, dimension.Level));
    }

    [Fact]
    public void Each_dimension_rolls_up_only_its_own_rules()
    {
        var vendor = new VendorBuilder()
            .WithFinancialHealth(30)                    // Financial: High
            .WithSlaUptime(90)                          // Operational: High
            .WithDocuments(pentestReportValid: false)   // SecurityCompliance: Critical
            .Build();

        var assessment = EngineFactory.Create().Evaluate(vendor);
        var byCategory = assessment.Dimensions.ToDictionary(dimension => dimension.Category);

        Assert.Equal(RiskLevel.High, byCategory[RiskCategory.Financial].Level);
        Assert.Equal(RiskLevel.High, byCategory[RiskCategory.Operational].Level);
        Assert.Equal(RiskLevel.Critical, byCategory[RiskCategory.SecurityCompliance].Level);
        Assert.All(assessment.Dimensions, dimension =>
            Assert.All(dimension.TriggeredRules, rule => Assert.Equal(dimension.Category, rule.Category)));
    }

    /// <summary>
    /// Pins the deferred numeric score. When the section 7 formula is implemented, this test is
    /// the one to replace with real expectations.
    /// </summary>
    [Fact]
    public void RiskScore_is_zero_on_every_path_until_the_numeric_engine_lands()
    {
        var engine = EngineFactory.Create();

        var vendors = new[]
        {
            VendorBuilder.Clean(),
            new VendorBuilder().WithCerts().Build(),
            new VendorBuilder().WithFinancialHealth(10).WithSlaUptime(50).WithMajorIncidents(9)
                .WithCerts().WithDocuments(false, false, false).Build()
        };

        foreach (var vendor in vendors)
        {
            var assessment = engine.Evaluate(vendor);

            Assert.Equal(0d, assessment.RiskScore);
            Assert.All(assessment.Dimensions, dimension => Assert.Equal(0d, dimension.Score));
        }
    }

    [Fact]
    public void Evaluation_carries_the_vendor_id_and_a_timestamp()
    {
        var vendor = new VendorBuilder().WithId(42).Build();

        var assessment = EngineFactory.Create().Evaluate(vendor);

        Assert.Equal(42, assessment.VendorId);
        Assert.Equal(DateTimeKind.Utc, assessment.EvaluatedAtUtc.Kind);
    }

    [Fact]
    public void An_engine_with_no_rules_still_produces_a_valid_assessment()
    {
        var assessment = EngineFactory.Create([]).Evaluate(VendorBuilder.Clean());

        Assert.Equal(RiskLevel.Low, assessment.RiskLevel);
        Assert.Equal(3, assessment.Dimensions.Count);
    }

    [Fact]
    public void Evaluate_rejects_a_null_vendor()
    {
        Assert.Throws<ArgumentNullException>(() => EngineFactory.Create().Evaluate(null!));
    }
}
