using VendorRisk.Domain.Risk;
using VendorRisk.Domain.Rules;
using VendorRisk.UnitTests.TestSupport;

namespace VendorRisk.UnitTests.Rules;

public class SecurityComplianceRuleTests
{
    [Fact]
    public void MissingIso27001_fires_when_the_certificate_is_absent()
    {
        var vendor = new VendorBuilder().WithCerts("SOC2").Build();

        var evaluation = new MissingIso27001Rule().Evaluate(vendor);

        Assert.NotNull(evaluation);
        Assert.Equal(RiskLevel.High, evaluation.Level);
        Assert.Equal(RiskCategory.SecurityCompliance, evaluation.Category);
        Assert.Equal("Missing ISO27001", evaluation.Explanation);
    }

    [Fact]
    public void MissingIso27001_fires_when_no_certificates_are_held()
    {
        var vendor = new VendorBuilder().WithCerts().Build();

        Assert.NotNull(new MissingIso27001Rule().Evaluate(vendor));
    }

    [Theory]
    [InlineData("ISO27001")]
    [InlineData("iso27001")]
    [InlineData("Iso27001")]
    public void MissingIso27001_matching_is_case_insensitive(string cert)
    {
        var vendor = new VendorBuilder().WithCerts(cert, "SOC2").Build();

        Assert.Null(new MissingIso27001Rule().Evaluate(vendor));
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void PrivacyPolicyExpired_follows_the_document_flag(bool privacyPolicyValid, bool shouldFire)
    {
        var vendor = new VendorBuilder().WithDocuments(privacyPolicyValid: privacyPolicyValid).Build();

        var evaluation = new PrivacyPolicyExpiredRule().Evaluate(vendor);

        Assert.Equal(shouldFire, evaluation is not null);
        if (evaluation is not null)
        {
            // Section 5 calls this "Moderate"; the four-level contract names it Medium.
            Assert.Equal(RiskLevel.Medium, evaluation.Level);
        }
    }

    /// <summary>
    /// The dataset has no pass/fail result, so an invalid or missing pentest report is treated as
    /// a failed penetration test. See the Missing Code Notice in the README.
    /// </summary>
    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void FailedPenTest_treats_an_invalid_report_as_a_failure(bool pentestReportValid, bool shouldFire)
    {
        var vendor = new VendorBuilder().WithDocuments(pentestReportValid: pentestReportValid).Build();

        var evaluation = new FailedPenTestRule().Evaluate(vendor);

        Assert.Equal(shouldFire, evaluation is not null);
        if (evaluation is not null)
        {
            Assert.Equal(RiskLevel.Critical, evaluation.Level);
        }
    }

    [Fact]
    public void ContractValid_has_no_rule_because_section_5_defines_none()
    {
        // An invalid contract on its own must not change the assessment: section 5 lists no
        // condition for it. Documented in the README so it reads as a decision, not an omission.
        var vendor = new VendorBuilder().WithDocuments(contractValid: false).Build();

        Assert.Null(new PrivacyPolicyExpiredRule().Evaluate(vendor));
        Assert.Null(new FailedPenTestRule().Evaluate(vendor));
        Assert.Null(new MissingIso27001Rule().Evaluate(vendor));
    }
}
