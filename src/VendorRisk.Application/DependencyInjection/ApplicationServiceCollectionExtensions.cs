using Microsoft.Extensions.DependencyInjection;
using VendorRisk.Application.Abstractions;
using VendorRisk.Application.Scoring;
using VendorRisk.Application.Services;
using VendorRisk.Domain.Risk;
using VendorRisk.Domain.Rules;

namespace VendorRisk.Application.DependencyInjection;

/// <summary>Registers the application services and the section 5 rule set.</summary>
public static class ApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<IRiskScoringEngine, RuleBasedRiskScoringEngine>();
        services.AddScoped<IVendorService, VendorService>();

        return services.AddRiskRules();
    }

    /// <summary>
    /// Registration order matters: it is the tie-break for rules of equal severity in the reason
    /// string, so rules are registered in the order they appear in case study section 5.
    /// </summary>
    private static IServiceCollection AddRiskRules(this IServiceCollection services)
    {
        services.AddSingleton<IRiskRule, LowFinancialHealthRule>();
        services.AddSingleton<IRiskRule, StrongFinancialHealthRule>();

        services.AddSingleton<IRiskRule, SlaBelowThresholdRule>();
        services.AddSingleton<IRiskRule, MajorIncidentsRule>();
        services.AddSingleton<IRiskRule, SlowTicketResolutionRule>();

        services.AddSingleton<IRiskRule, MissingIso27001Rule>();
        services.AddSingleton<IRiskRule, PrivacyPolicyExpiredRule>();
        services.AddSingleton<IRiskRule, FailedPenTestRule>();

        return services;
    }
}
