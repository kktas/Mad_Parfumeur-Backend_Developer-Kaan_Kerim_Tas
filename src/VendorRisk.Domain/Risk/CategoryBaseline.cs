using VendorRisk.Domain.Rules;
using VendorRisk.Domain.Vendors;

namespace VendorRisk.Domain.Risk;

/// <summary>
/// The graded part of a category's score, carried with the sentence that justifies it so no number
/// in an assessment is left unexplained.
/// </summary>
/// <param name="Value">Graded risk in 0..<see cref="RiskWeights.MaxBaseline"/>.</param>
/// <param name="Basis">Why it has that value, e.g. "Financial health 75 of 100".</param>
public sealed record CategoryBaseline(double Value, string Basis);

/// <summary>
/// Section 5 defines cliffs rather than curves, and leaves gaps between them - most importantly the
/// 50-80 financial band, in the category section 7 weights the heaviest. These baselines grade the
/// continuous inputs across their whole range, anchored on section 5's own thresholds, so that two
/// vendors the rules cannot tell apart are still ordered by the data. They are capped at
/// <see cref="RiskWeights.MaxBaseline"/> so a baseline never outweighs a real finding.
/// </summary>
public static class CategoryBaselines
{
    public static CategoryBaseline For(RiskCategory category, VendorProfile vendor)
    {
        ArgumentNullException.ThrowIfNull(vendor);

        return category switch
        {
            RiskCategory.Financial => Financial(vendor),
            RiskCategory.Operational => Operational(vendor),
            _ => SecurityCompliance()
        };
    }

    /// <summary>
    /// Zero at the "strong" threshold and at the cap by the "high risk" one: 80 earns nothing, 50
    /// and below earn the full baseline, and the band between is linear.
    /// </summary>
    private static CategoryBaseline Financial(VendorProfile vendor)
    {
        var span = (double)(RiskThresholds.StrongFinancialHealth - RiskThresholds.LowFinancialHealth);
        var distance = (RiskThresholds.StrongFinancialHealth - vendor.FinancialHealth) / span;

        return new CategoryBaseline(
            RiskWeights.MaxBaseline * RiskWeights.Clamp(distance),
            $"Financial health {vendor.FinancialHealth} of 100");
    }

    /// <summary>
    /// Incidents below section 5's "more than 2" bar are still evidence, so they are graded rather
    /// than ignored. A vendor with no incidents contributes nothing.
    /// </summary>
    private static CategoryBaseline Operational(VendorProfile vendor)
    {
        var counted = Math.Min(vendor.MajorIncidents, RiskWeights.IncidentBaselineWindow);
        var ratio = counted / (double)RiskWeights.IncidentBaselineWindow;

        return new CategoryBaseline(
            RiskWeights.MaxBaseline * RiskWeights.Clamp(ratio),
            $"{vendor.MajorIncidents} major incident(s) in the last 12 months");
    }

    /// <summary>
    /// Nothing to grade: every security and compliance input is a yes or a no - a certificate is
    /// held or it is not, a document is valid or it is not.
    /// </summary>
    private static CategoryBaseline SecurityCompliance() =>
        new(0d, "No graded inputs: certificates and documents are either held or not");
}
