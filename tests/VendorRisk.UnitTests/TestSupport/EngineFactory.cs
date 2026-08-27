using Microsoft.Extensions.Logging.Abstractions;
using VendorRisk.Application.Scoring;
using VendorRisk.Domain.Risk;
using VendorRisk.Domain.Rules;
using VendorRisk.Infrastructure.Scoring;

namespace VendorRisk.UnitTests.TestSupport;

public static class EngineFactory
{
    /// <summary>
    /// The production rule set in production registration order, so tests exercise the same
    /// ordering the API serves. Mirrors ApplicationServiceCollectionExtensions.AddRiskRules.
    /// </summary>
    public static IReadOnlyList<IRiskRule> AllRules() =>
    [
        new LowFinancialHealthRule(),
        new StrongFinancialHealthRule(),
        new SlaBelowThresholdRule(),
        new MajorIncidentsRule(),
        new SlowTicketResolutionRule(),
        new MissingIso27001Rule(),
        new PrivacyPolicyExpiredRule(),
        new FailedPenTestRule()
    ];

    /// <summary>
    /// The matrix the API ships, read from the test output. Tests score against the real appendix A
    /// data rather than a stand-in, so the coefficients they pin are the ones that go to production.
    /// </summary>
    public static IRiskFactorMatrix ShippedMatrix { get; } = JsonRiskFactorMatrix.Load(
        Path.Combine(AppContext.BaseDirectory, "data", "RiskFactorMatrix.json"),
        NullLogger<JsonRiskFactorMatrix>.Instance);

    public static RuleBasedRiskScoringEngine Create(
        IEnumerable<IRiskRule>? rules = null,
        IRiskFactorMatrix? matrix = null) =>
        new(rules ?? AllRules(), matrix ?? ShippedMatrix, NullLogger<RuleBasedRiskScoringEngine>.Instance);
}
