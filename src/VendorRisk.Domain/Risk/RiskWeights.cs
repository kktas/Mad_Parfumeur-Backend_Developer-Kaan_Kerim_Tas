namespace VendorRisk.Domain.Risk;

/// <summary>
/// Every number the numeric score depends on, in one place so the weighting cannot drift between
/// the rules, the engine and their tests. The case study fixes only the category weights in
/// section 7; everything else here is an assumption documented in the README's scoring section.
/// </summary>
public static class RiskWeights
{
    /// <summary>Impact a finding contributes for its severity, before any grading.</summary>
    public const double LowImpact = 0.10;
    public const double MediumImpact = 0.40;
    public const double HighImpact = 0.70;
    public const double CriticalImpact = 1.00;

    /// <summary>How far above its base impact a graded rule may climb, so it stays inside its band.</summary>
    public const double MaxEscalation = 0.30;

    /// <summary>
    /// Ceiling on a category's graded baseline. Capped at the Medium impact so a baseline alone can
    /// never outweigh an actual section 5 finding.
    /// </summary>
    public const double MaxBaseline = MediumImpact;

    /// <summary>SLA points below the section 5 target that earn the full escalation.</summary>
    public const decimal SlaEscalationWindow = 10m;

    /// <summary>Incidents above the section 5 threshold that earn the full escalation.</summary>
    public const int IncidentEscalationWindow = 3;

    /// <summary>Incidents that take the operational baseline to its cap.</summary>
    public const int IncidentBaselineWindow = 3;

    /// <summary>
    /// A risk the matrix implies is inferred rather than observed, so it counts half as much as the
    /// finding that implied it.
    /// </summary>
    public const double ImpliedRiskDamping = 0.5;

    /// <summary>Category weights from section 7 of the case study. These are the brief's, not ours.</summary>
    public const double FinancialWeight = 0.4;
    public const double OperationalWeight = 0.3;
    public const double SecurityComplianceWeight = 0.3;

    /// <summary>Lower bound of each risk level as a score.</summary>
    public const double MediumBand = 0.25;
    public const double HighBand = 0.50;
    public const double CriticalBand = 0.75;

    public static double ImpactOf(RiskLevel level) => level switch
    {
        RiskLevel.Critical => CriticalImpact,
        RiskLevel.High => HighImpact,
        RiskLevel.Medium => MediumImpact,
        _ => LowImpact
    };

    public static double WeightOf(RiskCategory category) => category switch
    {
        RiskCategory.Financial => FinancialWeight,
        RiskCategory.Operational => OperationalWeight,
        _ => SecurityComplianceWeight
    };

    /// <summary>The level a score alone implies, before it is compared with the triggered rules.</summary>
    public static RiskLevel LevelFor(double score) => score switch
    {
        >= CriticalBand => RiskLevel.Critical,
        >= HighBand => RiskLevel.High,
        >= MediumBand => RiskLevel.Medium,
        _ => RiskLevel.Low
    };

    /// <summary>Confines a ratio to 0..1, so a graded term never leaves its band.</summary>
    public static double Clamp(double value) => Math.Clamp(value, 0d, 1d);

    /// <summary>
    /// Scores are reported to two decimals, matching the case study's examples. Rounded through
    /// decimal on purpose: a total such as 0.745 is held as 0.74499... in binary and would round
    /// down, so a score would sit on one side or the other of a level band by an accident of
    /// floating point rather than by the arithmetic.
    /// </summary>
    public static double Round(double score) => (double)Math.Round((decimal)score, 2, MidpointRounding.AwayFromZero);
}
