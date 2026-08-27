namespace VendorRisk.Domain.Risk;

/// <summary>
/// Severity of a risk, ordered so that the overall level of an assessment is simply the maximum
/// of its triggered rules. Section 5 of the case study calls the middle level "Moderate";
/// it is named <see cref="Medium"/> here to match the four levels used by the API contract.
/// </summary>
public enum RiskLevel
{
    Low = 0,
    Medium = 1,
    High = 2,
    Critical = 3
}
