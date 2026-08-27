using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VendorRisk.Domain.Vendors;
using VendorRisk.Infrastructure.Persistence;

namespace VendorRisk.Infrastructure.Seeding;

/// <summary>
/// Loads the certificate catalogue and the sample vendors from case study appendix B on first run.
/// Idempotent: each table is filled only while it is empty, so restarts never duplicate or
/// overwrite live data.
/// </summary>
public sealed class DataSeeder
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly VendorRiskDbContext _dbContext;
    private readonly ILogger<DataSeeder> _logger;

    public DataSeeder(VendorRiskDbContext dbContext, ILogger<DataSeeder> logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task SeedAsync(
        string datasetPath,
        string certificateCatalogPath,
        CancellationToken cancellationToken = default)
    {
        // The catalogue is seeded first and independently: vendors reference it, and it stays
        // useful even once the vendor table has live rows in it.
        await SeedCertificateCatalogAsync(certificateCatalogPath, cancellationToken);
        await SeedVendorsAsync(datasetPath, cancellationToken);
    }

    private async Task SeedCertificateCatalogAsync(string catalogPath, CancellationToken cancellationToken)
    {
        if (await _dbContext.Certificates.AnyAsync(cancellationToken))
        {
            _logger.LogInformation("Certificate catalogue is not empty; skipping seed");
            return;
        }

        var catalog = await ReadAsync<SecurityCertificateDataFile>(catalogPath, cancellationToken);
        if (catalog is null || catalog.Certificates.Count == 0)
        {
            _logger.LogWarning("Certificate catalogue at {CatalogPath} was missing or empty", catalogPath);
            return;
        }

        var certificates = catalog.Certificates
            .Where(record => !string.IsNullOrWhiteSpace(record.Code))
            .Select(record => new SecurityCertificate
            {
                // Codes go through the same canonical form as anything posted to the API, so the
                // catalogue and the vendor payloads always agree.
                Code = SecurityCertificates.Normalise([record.Code]).Single(),
                Name = string.IsNullOrWhiteSpace(record.Name) ? record.Code.Trim() : record.Name.Trim(),
                Description = record.Description
            })
            .DistinctBy(certificate => certificate.Code, StringComparer.Ordinal)
            .ToList();

        _dbContext.Certificates.AddRange(certificates);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Seeded {CertificateCount} certificates from {CatalogPath}", certificates.Count, catalogPath);
    }

    private async Task SeedVendorsAsync(string datasetPath, CancellationToken cancellationToken)
    {
        if (await _dbContext.Vendors.AnyAsync(cancellationToken))
        {
            _logger.LogInformation("Vendor table is not empty; skipping seed");
            return;
        }

        var dataset = await ReadAsync<SampleVendorDataFile>(datasetPath, cancellationToken);
        if (dataset is null || dataset.Vendors.Count == 0)
        {
            _logger.LogWarning("Seed dataset at {DatasetPath} was missing or contained no vendors", datasetPath);
            return;
        }

        var catalog = await _dbContext.Certificates.ToDictionaryAsync(
            certificate => certificate.Code, StringComparer.Ordinal, cancellationToken);

        var nowUtc = DateTime.UtcNow;
        var vendors = dataset.Vendors.Select(record =>
        {
            var vendor = new VendorProfile
            {
                // Ids from the dataset are preserved so the README's example requests address the
                // same vendors the case study names.
                Id = record.Id,
                Name = record.Name,
                FinancialHealth = record.FinancialHealth,
                SlaUptime = record.SlaUptime,
                MajorIncidents = record.MajorIncidents,
                Documents = new VendorDocuments
                {
                    ContractValid = record.Documents.ContractValid,
                    PrivacyPolicyValid = record.Documents.PrivacyPolicyValid,
                    PentestReportValid = record.Documents.PentestReportValid
                },
                CreatedAtUtc = nowUtc,
                UpdatedAtUtc = nowUtc
            };

            vendor.SetCertificates(ResolveCertificates(record.SecurityCerts, catalog));

            return vendor;
        }).ToList();

        _dbContext.Vendors.AddRange(vendors);
        await _dbContext.SaveChangesAsync(cancellationToken);

        // Explicit ids bypass the identity sequence, so realign it or the next insert collides.
        await ResetIdentitySequenceAsync(cancellationToken);

        _logger.LogInformation("Seeded {VendorCount} vendors from {DatasetPath}", vendors.Count, datasetPath);
    }

    /// <summary>
    /// Maps the codes on a sample vendor onto catalogue rows, adding a bare entry for any code the
    /// catalogue does not describe. New entries are tracked and saved with the vendors.
    /// </summary>
    private IEnumerable<SecurityCertificate> ResolveCertificates(
        IEnumerable<string> codes,
        Dictionary<string, SecurityCertificate> catalog)
    {
        foreach (var code in SecurityCertificates.Normalise(codes))
        {
            if (!catalog.TryGetValue(code, out var certificate))
            {
                _logger.LogWarning("Sample data holds certificate {CertificateCode}, which the catalogue does not describe; registering it", code);

                certificate = new SecurityCertificate { Code = code, Name = code };
                _dbContext.Certificates.Add(certificate);
                catalog[code] = certificate;
            }

            yield return certificate;
        }
    }

    private async Task<T?> ReadAsync<T>(string path, CancellationToken cancellationToken)
    {
        if (!File.Exists(path))
        {
            _logger.LogWarning("Seed file not found at {Path}", path);
            return default;
        }

        await using var stream = File.OpenRead(path);

        return await JsonSerializer.DeserializeAsync<T>(stream, SerializerOptions, cancellationToken);
    }

    private async Task ResetIdentitySequenceAsync(CancellationToken cancellationToken) =>
        await _dbContext.Database.ExecuteSqlRawAsync(
            """SELECT setval(pg_get_serial_sequence('vendors', 'Id'), COALESCE((SELECT MAX("Id") FROM vendors), 1));""",
            cancellationToken);
}
