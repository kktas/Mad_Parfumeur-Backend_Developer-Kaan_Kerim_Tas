using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VendorRisk.Domain.Vendors;
using VendorRisk.Infrastructure.Persistence;

namespace VendorRisk.Infrastructure.Seeding;

/// <summary>
/// Loads the sample vendors from case study appendix B on first run. Idempotent: it does nothing
/// once the table holds any rows, so restarts never duplicate or overwrite live data.
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

    public async Task SeedAsync(string datasetPath, CancellationToken cancellationToken = default)
    {
        if (await _dbContext.Vendors.AnyAsync(cancellationToken))
        {
            _logger.LogInformation("Vendor table is not empty; skipping seed");
            return;
        }

        if (!File.Exists(datasetPath))
        {
            _logger.LogWarning("Seed dataset not found at {DatasetPath}; starting with an empty vendor table", datasetPath);
            return;
        }

        await using var stream = File.OpenRead(datasetPath);
        var dataset = await JsonSerializer.DeserializeAsync<SampleVendorDataFile>(stream, SerializerOptions, cancellationToken);

        if (dataset is null || dataset.Vendors.Count == 0)
        {
            _logger.LogWarning("Seed dataset at {DatasetPath} contained no vendors", datasetPath);
            return;
        }

        var nowUtc = DateTime.UtcNow;
        var vendors = dataset.Vendors.Select(record => new VendorProfile
        {
            // Ids from the dataset are preserved so the README's example requests address the
            // same vendors the case study names.
            Id = record.Id,
            Name = record.Name,
            FinancialHealth = record.FinancialHealth,
            SlaUptime = record.SlaUptime,
            MajorIncidents = record.MajorIncidents,
            // Seeded rows go through the same canonical form as anything posted to the API.
            SecurityCerts = SecurityCertificates.Normalise(record.SecurityCerts),
            Documents = new VendorDocuments
            {
                ContractValid = record.Documents.ContractValid,
                PrivacyPolicyValid = record.Documents.PrivacyPolicyValid,
                PentestReportValid = record.Documents.PentestReportValid
            },
            CreatedAtUtc = nowUtc,
            UpdatedAtUtc = nowUtc
        }).ToList();

        _dbContext.Vendors.AddRange(vendors);
        await _dbContext.SaveChangesAsync(cancellationToken);

        // Explicit ids bypass the identity sequence, so realign it or the next insert collides.
        await ResetIdentitySequenceAsync(cancellationToken);

        _logger.LogInformation("Seeded {VendorCount} vendors from {DatasetPath}", vendors.Count, datasetPath);
    }

    private async Task ResetIdentitySequenceAsync(CancellationToken cancellationToken) =>
        await _dbContext.Database.ExecuteSqlRawAsync(
            """SELECT setval(pg_get_serial_sequence('vendors', 'Id'), COALESCE((SELECT MAX("Id") FROM vendors), 1));""",
            cancellationToken);
}
