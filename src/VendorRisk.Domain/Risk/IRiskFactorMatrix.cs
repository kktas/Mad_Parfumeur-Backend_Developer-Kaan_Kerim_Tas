namespace VendorRisk.Domain.Risk;

/// <summary>
/// The risk similarity matrix from appendix A: for a risk item, the items it tends to come with.
/// </summary>
public interface IRiskFactorMatrix
{
    /// <summary>
    /// Risks similar to <paramref name="node"/>, or an empty list when the matrix has no entry for
    /// it. Many neighbours have no entry of their own - they are named as similar risks without
    /// being described further - so an empty result is normal, not an error.
    /// </summary>
    IReadOnlyList<MatrixNeighbour> Related(string node);
}

/// <summary>One edge of the matrix: a similar risk and how strongly it is associated (0..1).</summary>
public sealed record MatrixNeighbour(string Node, double Similarity);

/// <summary>A matrix with no entries. Scoring falls back to observed findings only.</summary>
public sealed class EmptyRiskFactorMatrix : IRiskFactorMatrix
{
    public static readonly EmptyRiskFactorMatrix Instance = new();

    public IReadOnlyList<MatrixNeighbour> Related(string node) => [];
}
