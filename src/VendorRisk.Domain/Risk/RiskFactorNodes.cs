namespace VendorRisk.Domain.Risk;

/// <summary>
/// The RiskFactorMatrix.json entries the section 5 rules observe. Node names are unique across the
/// file's four groups, so a bare name identifies an entry unambiguously.
/// </summary>
public static class RiskFactorNodes
{
    /// <summary>
    /// The matrix has no entry for a financial health score as such. Its general financial-distress
    /// entry is the closest match, which is an assumption recorded in the README.
    /// </summary>
    public const string LowCashFlow = "lowCashFlow";

    public const string SlaDrop = "slaDrop";
    public const string MajorIncident = "majorIncident";
    public const string SlowTicketResolution = "slowTicketResolution";
    public const string MissingIso27001 = "missingISO27001";
    public const string FailedPenTest = "failedPenTest";
    public const string ExpiredPrivacyPolicy = "expiredPrivacyPolicy";
}
