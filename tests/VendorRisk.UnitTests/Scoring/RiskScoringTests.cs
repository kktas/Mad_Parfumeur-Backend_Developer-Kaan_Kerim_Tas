using VendorRisk.Domain.Risk;
using VendorRisk.Domain.Rules;
using VendorRisk.Domain.Vendors;
using VendorRisk.UnitTests.TestSupport;

namespace VendorRisk.UnitTests.Scoring;

/// <summary>
/// The arithmetic behind the score, piece by piece: how far a finding escalates inside its band,
/// what the graded baselines contribute, and how the similarity matrix lifts a dimension. The
/// assumptions these pin are the ones set out in the README's scoring section.
/// </summary>
public class RiskScoringTests
{
    private static double DimensionScore(VendorProfile vendor, RiskCategory category) =>
        EngineFactory.Create().Evaluate(vendor).Dimensions.Single(d => d.Category == category).Score;

    private static IReadOnlyList<RelatedRisk> RelatedRisks(VendorProfile vendor, RiskCategory category) =>
        EngineFactory.Create().Evaluate(vendor).Dimensions.Single(d => d.Category == category).RelatedRisks;

    // --- graded impacts -----------------------------------------------------------------------

    [Theory]
    [InlineData(49, 0.706)]   // a hair under the threshold: barely more than the High base
    [InlineData(25, 0.85)]
    [InlineData(0, 1.0)]      // the floor of the scale reaches the cap
    public void Low_financial_health_escalates_with_the_shortfall(int financialHealth, double expected)
    {
        var evaluation = new LowFinancialHealthRule().Evaluate(
            new VendorBuilder().WithFinancialHealth(financialHealth).Build());

        Assert.NotNull(evaluation);
        Assert.Equal(expected, evaluation.Impact, 3);
    }

    [Theory]
    [InlineData(94.9, 0.703)]
    [InlineData(90, 0.85)]
    [InlineData(85, 1.0)]     // a full escalation window below the target
    [InlineData(60, 1.0)]     // and no further: impacts are capped at 1
    public void Sla_shortfall_escalates_and_then_caps(decimal slaUptime, double expected)
    {
        var evaluation = new SlaBelowThresholdRule().Evaluate(new VendorBuilder().WithSlaUptime(slaUptime).Build());

        Assert.NotNull(evaluation);
        Assert.Equal(expected, evaluation.Impact, 3);
    }

    [Theory]
    [InlineData(3, 0.8)]
    [InlineData(5, 1.0)]
    [InlineData(50, 1.0)]
    public void Major_incidents_escalate_with_the_count(int incidents, double expected)
    {
        var evaluation = new MajorIncidentsRule().Evaluate(new VendorBuilder().WithMajorIncidents(incidents).Build());

        Assert.NotNull(evaluation);
        Assert.Equal(expected, evaluation.Impact, 3);
    }

    [Fact]
    public void A_binary_rule_contributes_its_severity_unchanged()
    {
        var vendor = new VendorBuilder().WithCerts().WithDocuments(privacyPolicyValid: false, pentestReportValid: false).Build();

        Assert.Equal(RiskWeights.HighImpact, new MissingIso27001Rule().Evaluate(vendor)!.Impact);
        Assert.Equal(RiskWeights.MediumImpact, new PrivacyPolicyExpiredRule().Evaluate(vendor)!.Impact);
        Assert.Equal(RiskWeights.CriticalImpact, new FailedPenTestRule().Evaluate(vendor)!.Impact);
    }

    [Fact]
    public void A_favourable_finding_adds_no_risk()
    {
        var vendor = new VendorBuilder().WithFinancialHealth(95).Build();
        var evaluation = new StrongFinancialHealthRule().Evaluate(vendor);

        Assert.NotNull(evaluation);
        Assert.Equal(0d, evaluation.Impact);
        Assert.Null(evaluation.MatrixNode);
        // Strong finances, no incidents: nothing at all to score.
        Assert.Equal(0d, DimensionScore(vendor, RiskCategory.Financial));
    }

    // --- graded baselines ---------------------------------------------------------------------

    [Theory]
    [InlineData(90, 0.00)]    // above the "strong" threshold
    [InlineData(80, 0.00)]    // exactly on it
    [InlineData(70, 0.13)]
    [InlineData(60, 0.27)]
    [InlineData(50, 0.40)]    // the cap, where the section 5 rule takes over
    public void The_financial_baseline_grades_the_band_section_5_leaves_empty(int financialHealth, double expected)
    {
        var vendor = new VendorBuilder().WithFinancialHealth(financialHealth).Build();

        Assert.Equal(expected, DimensionScore(vendor, RiskCategory.Financial));
        Assert.Empty(EngineFactory.Create().Evaluate(vendor)
            .Dimensions.Single(d => d.Category == RiskCategory.Financial)
            .TriggeredRules.Where(rule => rule.Impact > 0));
    }

    [Theory]
    [InlineData(0, 0.00)]
    [InlineData(1, 0.13)]
    [InlineData(2, 0.27)]
    public void Incidents_below_the_threshold_still_count(int incidents, double expected)
    {
        // SLA is above target, so the operational score is the baseline alone.
        var vendor = new VendorBuilder().WithMajorIncidents(incidents).Build();

        Assert.Equal(expected, DimensionScore(vendor, RiskCategory.Operational));
    }

    [Fact]
    public void Security_and_compliance_have_nothing_to_grade()
    {
        var dimension = EngineFactory.Create().Evaluate(VendorBuilder.Clean())
            .Dimensions.Single(d => d.Category == RiskCategory.SecurityCompliance);

        Assert.Equal(0d, dimension.Baseline.Value);
        Assert.NotEmpty(dimension.Baseline.Basis);
    }

    [Fact]
    public void Every_baseline_explains_itself()
    {
        var assessment = EngineFactory.Create().Evaluate(new VendorBuilder().WithFinancialHealth(75).Build());

        Assert.All(assessment.Dimensions, dimension => Assert.NotEmpty(dimension.Baseline.Basis));
        Assert.Contains("75", assessment.Dimensions
            .Single(d => d.Category == RiskCategory.Financial).Baseline.Basis);
    }

    // --- combining findings -------------------------------------------------------------------

    [Fact]
    public void Findings_accumulate_without_ever_passing_one()
    {
        // Missing ISO27001 (0.70) and an expired privacy policy (0.40) combine to 0.82 before the
        // matrix lifts it - more than either alone, less than their sum.
        var vendor = new VendorBuilder().WithCerts().WithDocuments(privacyPolicyValid: false).Build();
        var onlyCertificate = new VendorBuilder().WithCerts().Build();

        var both = DimensionScore(vendor, RiskCategory.SecurityCompliance);
        var one = DimensionScore(onlyCertificate, RiskCategory.SecurityCompliance);

        Assert.True(both > one);
        Assert.True(both <= 1d);
    }

    [Fact]
    public void A_critical_finding_saturates_its_dimension()
    {
        var vendor = new VendorBuilder().WithDocuments(pentestReportValid: false).Build();

        Assert.Equal(1d, DimensionScore(vendor, RiskCategory.SecurityCompliance));
    }

    // --- the similarity matrix ----------------------------------------------------------------

    [Fact]
    public void A_finding_implies_the_risks_the_matrix_associates_with_it()
    {
        var vendor = new VendorBuilder().WithSlaUptime(90).Build();

        var related = RelatedRisks(vendor, RiskCategory.Operational);

        // slaDrop -> downtime 0.87, slowTicketResolution 0.83, serviceInstability 0.79.
        Assert.Equal(["downtime", "slowTicketResolution", "serviceInstability"],
            related.Select(risk => risk.Node));
        Assert.Equal(0.87, related[0].Similarity);
        // The SLA finding is 0.85 at 90%, so the strongest implication is 0.85 x 0.87.
        Assert.Equal(0.74, related[0].ImpliedImpact);
        Assert.Equal("SlaBelowThreshold", related[0].SourceRuleId);
    }

    [Fact]
    public void The_strongest_implication_lifts_the_dimension_toward_one()
    {
        var vendor = new VendorBuilder().WithSlaUptime(90).Build();

        var withMatrix = DimensionScore(vendor, RiskCategory.Operational);
        var withoutMatrix = new RuleBasedRiskScoringEngineHarness(EmptyRiskFactorMatrix.Instance)
            .OperationalScore(vendor);

        // 0.85 observed, lifted by half of the strongest implication across the remaining 0.15.
        Assert.Equal(0.91, withMatrix);
        Assert.Equal(0.85, withoutMatrix);
    }

    [Fact]
    public void An_implication_cannot_push_a_saturated_dimension_past_one()
    {
        // Failed pentest implies internalVulnerabilities at 0.88, but there is no room left.
        var vendor = new VendorBuilder().WithDocuments(pentestReportValid: false).Build();

        Assert.NotEmpty(RelatedRisks(vendor, RiskCategory.SecurityCompliance));
        Assert.Equal(1d, DimensionScore(vendor, RiskCategory.SecurityCompliance));
    }

    [Fact]
    public void A_risk_already_observed_is_not_also_implied()
    {
        // slaDrop implies slowTicketResolution, and majorIncident implies securityIncident; neither
        // may re-list a node this dimension already observes.
        var vendor = new VendorBuilder().WithSlaUptime(90).WithMajorIncidents(4).Build();

        var related = RelatedRisks(vendor, RiskCategory.Operational);

        Assert.DoesNotContain(related, risk => risk.Node is "slaDrop" or "majorIncident");
    }

    [Fact]
    public void Where_two_findings_imply_the_same_risk_the_stronger_one_wins()
    {
        var vendor = new VendorBuilder().WithSlaUptime(85).WithMajorIncidents(9).Build();

        var related = RelatedRisks(vendor, RiskCategory.Operational);

        Assert.Equal(related.Select(risk => risk.Node).Distinct().Count(), related.Count);
        Assert.Equal(related.OrderByDescending(risk => risk.ImpliedImpact).Select(risk => risk.Node),
            related.Select(risk => risk.Node));
    }

    [Fact]
    public void Without_a_matrix_scoring_falls_back_to_what_was_observed()
    {
        var vendor = new VendorBuilder().WithSlaUptime(90).WithCerts().Build();

        var assessment = EngineFactory.Create(matrix: EmptyRiskFactorMatrix.Instance).Evaluate(vendor);

        Assert.All(assessment.Dimensions, dimension => Assert.Empty(dimension.RelatedRisks));
        Assert.InRange(assessment.RiskScore, 0d, 1d);
    }

    // --- weights ------------------------------------------------------------------------------

    [Fact]
    public void The_category_weights_are_the_ones_section_7_gives()
    {
        Assert.Equal(0.4, RiskWeights.FinancialWeight);
        Assert.Equal(0.3, RiskWeights.OperationalWeight);
        Assert.Equal(0.3, RiskWeights.SecurityComplianceWeight);
        Assert.Equal(1d,
            RiskWeights.FinancialWeight + RiskWeights.OperationalWeight + RiskWeights.SecurityComplianceWeight,
            10);
    }

    [Fact]
    public void Level_bands_run_in_order()
    {
        Assert.Equal(RiskLevel.Low, RiskWeights.LevelFor(0.24));
        Assert.Equal(RiskLevel.Medium, RiskWeights.LevelFor(0.25));
        Assert.Equal(RiskLevel.High, RiskWeights.LevelFor(0.50));
        Assert.Equal(RiskLevel.Critical, RiskWeights.LevelFor(0.75));
        Assert.Equal(RiskLevel.Critical, RiskWeights.LevelFor(1d));
    }

    /// <summary>Scores one dimension with a matrix of the caller's choosing.</summary>
    private sealed class RuleBasedRiskScoringEngineHarness(IRiskFactorMatrix matrix)
    {
        public double OperationalScore(VendorProfile vendor) =>
            EngineFactory.Create(matrix: matrix).Evaluate(vendor)
                .Dimensions.Single(d => d.Category == RiskCategory.Operational).Score;
    }
}
