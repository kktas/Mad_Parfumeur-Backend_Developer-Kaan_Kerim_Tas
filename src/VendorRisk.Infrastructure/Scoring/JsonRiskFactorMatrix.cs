using System.Text.Json;
using Microsoft.Extensions.Logging;
using VendorRisk.Domain.Risk;

namespace VendorRisk.Infrastructure.Scoring;

/// <summary>
/// The similarity matrix from appendix A, read from data/RiskFactorMatrix.json once at startup and
/// held immutably for the life of the process.
/// </summary>
/// <remarks>
/// The file groups its entries as financialRisk / operationalRisk / securityRisk / complianceRisk,
/// but the groups are only containers: node names are unique across all four, so they are flattened
/// into a single lookup. Note that most neighbours named in the file have no entry of their own -
/// "weakAccessControl" is described, "internalVulnerabilities" is only ever pointed at - which is
/// expected: scoring needs a neighbour's coefficient, not its own row.
/// </remarks>
public sealed class JsonRiskFactorMatrix : IRiskFactorMatrix
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly IReadOnlyDictionary<string, IReadOnlyList<MatrixNeighbour>> _byNode;

    private JsonRiskFactorMatrix(IReadOnlyDictionary<string, IReadOnlyList<MatrixNeighbour>> byNode)
    {
        _byNode = byNode;
    }

    public IReadOnlyList<MatrixNeighbour> Related(string node) =>
        _byNode.TryGetValue(node, out var neighbours) ? neighbours : [];

    /// <summary>
    /// Loads the matrix, or falls back to an empty one. A missing or malformed file is a warning
    /// rather than a startup failure: without it every score simply loses its implied-risk uplift,
    /// which is a degraded assessment rather than no assessment at all.
    /// </summary>
    public static IRiskFactorMatrix Load(string path, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(logger);

        if (!File.Exists(path))
        {
            logger.LogWarning(
                "Risk factor matrix not found at {MatrixPath}; scoring will use observed findings only", path);

            return EmptyRiskFactorMatrix.Instance;
        }

        try
        {
            var groups = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, Dictionary<string, double>>>>(
                File.ReadAllText(path), SerializerOptions);

            if (groups is null || groups.Count == 0)
            {
                logger.LogWarning("Risk factor matrix at {MatrixPath} was empty", path);

                return EmptyRiskFactorMatrix.Instance;
            }

            return new JsonRiskFactorMatrix(Flatten(groups, path, logger));
        }
        catch (JsonException ex)
        {
            logger.LogWarning(
                ex, "Risk factor matrix at {MatrixPath} could not be read; scoring will use observed findings only", path);

            return EmptyRiskFactorMatrix.Instance;
        }
    }

    private static Dictionary<string, IReadOnlyList<MatrixNeighbour>> Flatten(
        Dictionary<string, Dictionary<string, Dictionary<string, double>>> groups,
        string path,
        ILogger logger)
    {
        var byNode = new Dictionary<string, IReadOnlyList<MatrixNeighbour>>(StringComparer.Ordinal);

        foreach (var (groupName, nodes) in groups)
        {
            foreach (var (node, neighbours) in nodes)
            {
                if (byNode.ContainsKey(node))
                {
                    // Names are unique across the shipped file; if that ever stops being true the
                    // first entry wins and the collision is surfaced rather than silently merged.
                    logger.LogWarning(
                        "Risk factor matrix at {MatrixPath} defines {Node} more than once ({Group} ignored)",
                        path, node, groupName);

                    continue;
                }

                byNode[node] =
                [
                    .. neighbours
                        .Select(neighbour => new MatrixNeighbour(neighbour.Key, neighbour.Value))
                        .OrderByDescending(neighbour => neighbour.Similarity)
                        .ThenBy(neighbour => neighbour.Node, StringComparer.Ordinal)
                ];
            }
        }

        logger.LogInformation("Loaded {NodeCount} risk factor matrix entries from {MatrixPath}", byNode.Count, path);

        return byNode;
    }
}
