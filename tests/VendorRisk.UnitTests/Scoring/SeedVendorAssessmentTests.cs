using System.Text.Json;
using VendorRisk.Application.Scoring;
using VendorRisk.Domain.Risk;
using VendorRisk.Domain.Vendors;
using VendorRisk.Infrastructure.Seeding;
using VendorRisk.UnitTests.TestSupport;

namespace VendorRisk.UnitTests.Scoring;

/// <summary>
/// Regression net over the shipped dataset (case study appendix B). It pins what every seeded
/// vendor assesses as, which makes the Critical-heavy distribution visible rather than surprising:
/// 10 of the 15 sample vendors have pentestReportValid = false, and that rule is Critical.
///
/// The scores are what separate those ten vendors from one another, and they are pinned here
/// because they are the calibration: changing a weight, a baseline or the damping factor moves
/// these numbers, and that should never happen silently.
/// </summary>
public class SeedVendorAssessmentTests
{
    private static readonly Lazy<IReadOnlyDictionary<int, VendorProfile>> SeedVendors = new(LoadSeedVendors);

    [Theory]
    // Only vendor 1 lands on High: its pentest report is valid, so nothing Critical fires.
    [InlineData(1, 0.42, "High", "SLA below 95% (High) + Privacy policy expired (Medium)")]
    [InlineData(2, 0.73, "Critical", "Failed penetration test (Critical) + SLA below 95% (High) + More than 2 major incidents in the last 12 months (High) + Missing ISO27001 (High)")]
    [InlineData(3, 0.00, "Low", "Strong financial health above 80 (Low)")]
    [InlineData(4, 0.68, "Critical", "Failed penetration test (Critical) + SLA below 95% (High) + Missing ISO27001 (High) + Privacy policy expired (Medium)")]
    [InlineData(5, 0.95, "Critical", "Failed penetration test (Critical) + Financial health below 50 (High) + SLA below 95% (High) + More than 2 major incidents in the last 12 months (High) + Missing ISO27001 (High) + Privacy policy expired (Medium)")]
    [InlineData(6, 0.39, "Critical", "Failed penetration test (Critical) + Missing ISO27001 (High)")]
    [InlineData(7, 0.00, "Low", "Strong financial health above 80 (Low)")]
    [InlineData(8, 0.96, "Critical", "Failed penetration test (Critical) + Financial health below 50 (High) + SLA below 95% (High) + Missing ISO27001 (High) + Privacy policy expired (Medium)")]
    [InlineData(9, 0.68, "Critical", "Failed penetration test (Critical) + SLA below 95% (High) + Missing ISO27001 (High)")]
    // Vendor 10 trips nothing at all: financial health sits inside the 50-80 band, SLA is above 95,
    // no incidents, ISO27001 held, every document valid.
    [InlineData(10, 0.03, "Low", RuleBasedRiskScoringEngine.NoFindingsReason)]
    [InlineData(11, 0.75, "Critical", "Failed penetration test (Critical) + SLA below 95% (High) + More than 2 major incidents in the last 12 months (High) + Missing ISO27001 (High) + Privacy policy expired (Medium)")]
    [InlineData(12, 0.55, "Critical", "Failed penetration test (Critical) + SLA below 95% (High) + Strong financial health above 80 (Low)")]
    [InlineData(13, 0.67, "Critical", "Failed penetration test (Critical) + SLA below 95% (High) + Missing ISO27001 (High) + Privacy policy expired (Medium)")]
    [InlineData(14, 0.00, "Low", "Strong financial health above 80 (Low)")]
    [InlineData(15, 0.66, "Critical", "Failed penetration test (Critical) + SLA below 95% (High) + Missing ISO27001 (High)")]
    public void Seed_vendors_assess_as_expected(
        int vendorId, double expectedScore, string expectedLevel, string expectedReason)
    {
        var vendor = SeedVendors.Value[vendorId];

        var assessment = EngineFactory.Create().Evaluate(vendor);

        Assert.Equal(expectedLevel, assessment.RiskLevel.ToString());
        Assert.Equal(expectedReason, assessment.Reason);
        Assert.Equal(expectedScore, assessment.RiskScore);
    }

    [Fact]
    public void The_shipped_dataset_holds_the_fifteen_vendors_from_appendix_b()
    {
        Assert.Equal(15, SeedVendors.Value.Count);
        Assert.Equal("TechPlus Solutions", SeedVendors.Value[1].Name);
    }

    [Fact]
    public void Ten_of_the_fifteen_seed_vendors_are_critical_because_of_the_pentest_rule()
    {
        var engine = EngineFactory.Create();

        var levels = SeedVendors.Value.Values
            .GroupBy(vendor => engine.Evaluate(vendor).RiskLevel)
            .ToDictionary(group => group.Key, group => group.Count());

        Assert.Equal(10, levels[RiskLevel.Critical]);
        Assert.Equal(1, levels[RiskLevel.High]);
        Assert.Equal(4, levels[RiskLevel.Low]);
        Assert.False(levels.ContainsKey(RiskLevel.Medium));
    }

    private static IReadOnlyDictionary<int, VendorProfile> LoadSeedVendors()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "data", "SampleVendorData.json");
        var dataset = JsonSerializer.Deserialize<SampleVendorDataFile>(
            File.ReadAllText(path),
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.NotNull(dataset);

        return dataset.Vendors.ToDictionary(
            record => record.Id,
            record =>
            {
                var vendor = new VendorProfile
                {
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
                    }
                };

                // Stands in for the seeder's catalogue lookup: the codes on the sample record
                // become the certificate rows the vendor is linked to.
                vendor.SetCertificates(SecurityCertificates
                    .Normalise(record.SecurityCerts)
                    .Select(code => new SecurityCertificate { Code = code, Name = code }));

                return vendor;
            });
    }
}
