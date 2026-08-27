namespace VendorRisk.Domain.Rules;

/// <summary>
/// Boundaries taken verbatim from section 5 of the case study, kept in one place so the rules and
/// their tests cannot drift apart.
/// </summary>
public static class RiskThresholds
{
    /// <summary>"FinancialHealth &lt; 50" is a high risk.</summary>
    public const int LowFinancialHealth = 50;

    /// <summary>"FinancialHealth &gt; 80" is a low risk.</summary>
    public const int StrongFinancialHealth = 80;

    /// <summary>"SLA uptime &lt; 95%" is a high operational risk.</summary>
    public const decimal MinimumSlaUptime = 95m;

    /// <summary>"Major incidents &gt; 2 (last 12 months)" is a high risk.</summary>
    public const int MaxAcceptableMajorIncidents = 2;

    /// <summary>Certification the security rules require.</summary>
    public const string RequiredCertification = "ISO27001";
}
