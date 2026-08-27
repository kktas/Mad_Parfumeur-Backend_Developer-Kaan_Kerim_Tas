namespace VendorRisk.Domain.Risk;

/// <summary>
/// The three dimensions a vendor is assessed across (case study section 2.2).
/// </summary>
public enum RiskCategory
{
    Financial = 0,
    Operational = 1,
    SecurityCompliance = 2
}
