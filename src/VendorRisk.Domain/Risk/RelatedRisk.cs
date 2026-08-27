namespace VendorRisk.Domain.Risk;

/// <summary>
/// A risk the matrix implies from something actually observed on the vendor.
/// </summary>
/// <param name="Node">The implied risk item, e.g. "weakAccessControl".</param>
/// <param name="Similarity">The matrix coefficient linking it to the observed finding.</param>
/// <param name="ImpliedImpact">The observed finding's impact scaled by that coefficient.</param>
/// <param name="SourceRuleId">The rule whose finding implied it.</param>
public sealed record RelatedRisk(string Node, double Similarity, double ImpliedImpact, string SourceRuleId);
