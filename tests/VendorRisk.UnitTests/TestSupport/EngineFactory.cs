using Microsoft.Extensions.Logging.Abstractions;
using VendorRisk.Application.Scoring;
using VendorRisk.Domain.Risk;
using VendorRisk.Domain.Rules;

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

    public static RuleBasedRiskScoringEngine Create(IEnumerable<IRiskRule>? rules = null) =>
        new(rules ?? AllRules(), NullLogger<RuleBasedRiskScoringEngine>.Instance);
}
