using Microsoft.Extensions.Configuration;

namespace VendorRisk.Infrastructure;

/// <summary>
/// Resolves the shipped data files. Configured paths may be absolute, or relative to the content
/// root as the defaults are.
/// </summary>
public static class DataPaths
{
    public const string SeedDatasetKey = "Database:SeedDatasetPath";
    public const string SeedDatasetDefault = "data/SampleVendorData.json";

    public const string CertificateCatalogKey = "Database:SeedCertificateCatalogPath";
    public const string CertificateCatalogDefault = "data/SecurityCertificates.json";

    public const string RiskFactorMatrixKey = "Scoring:RiskFactorMatrixPath";
    public const string RiskFactorMatrixDefault = "data/RiskFactorMatrix.json";

    public static string Resolve(IConfiguration configuration, string key, string fallback, string contentRootPath)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var configured = configuration[key] ?? fallback;

        return Path.IsPathRooted(configured) ? configured : Path.Combine(contentRootPath, configured);
    }
}
