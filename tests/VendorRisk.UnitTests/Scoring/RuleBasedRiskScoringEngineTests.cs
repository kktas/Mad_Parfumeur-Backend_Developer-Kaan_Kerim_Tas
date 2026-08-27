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

    [Fact]
    public void The_score_is_the_section_7_weighted_sum_of_the_dimensions()
    {
        var assessment = EngineFactory.Create().Evaluate(
            new VendorBuilder().WithSlaUptime(93).WithDocuments(privacyPolicyValid: false).Build());

        var byCategory = assessment.Dimensions.ToDictionary(dimension => dimension.Category);
        var expected = Math.Round(
            (0.4 * byCategory[RiskCategory.Financial].Score)
            + (0.3 * byCategory[RiskCategory.Operational].Score)
            + (0.3 * byCategory[RiskCategory.SecurityCompliance].Score),
            2);

        Assert.Equal(expected, assessment.RiskScore);
    }

    [Fact]
    public void Every_score_stays_within_zero_and_one()
    {
        var engine = EngineFactory.Create();

        var vendors = new[]
        {
            VendorBuilder.Clean(),
            new VendorBuilder().WithCerts().Build(),
            // Everything wrong, well past every threshold: the score must still be bounded.
            new VendorBuilder().WithFinancialHealth(0).WithSlaUptime(0).WithMajorIncidents(99)
                .WithCerts().WithDocuments(false, false, false).Build()
        };

        foreach (var vendor in vendors)
        {
            var assessment = engine.Evaluate(vendor);

            Assert.InRange(assessment.RiskScore, 0d, 1d);
            Assert.All(assessment.Dimensions, dimension => Assert.InRange(dimension.Score, 0d, 1d));
        }
    }

    [Fact]
    public void A_worse_vendor_never_scores_lower()
    {
        var engine = EngineFactory.Create();

        var better = engine.Evaluate(new VendorBuilder().WithSlaUptime(94).WithMajorIncidents(1).Build());
        var worse = engine.Evaluate(new VendorBuilder().WithSlaUptime(88).WithMajorIncidents(4).Build());

        Assert.True(worse.RiskScore > better.RiskScore);
    }

    [Fact]
    public void The_score_can_raise_the_level_but_never_lower_it()
    {
        // One Critical finding on an otherwise sound vendor scores well below the Critical band,
        // and must still be reported as Critical.
        var assessment = EngineFactory.Create().Evaluate(
            new VendorBuilder().WithDocuments(pentestReportValid: false).Build());

        Assert.Equal(RiskLevel.Critical, assessment.RiskLevel);
        Assert.True(assessment.RiskScore < RiskWeights.CriticalBand);
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
