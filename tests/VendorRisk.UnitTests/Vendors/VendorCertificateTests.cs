using System.Text.Json;
using VendorRisk.Domain.Rules;
using VendorRisk.Domain.Vendors;
using VendorRisk.Infrastructure.Seeding;
using VendorRisk.UnitTests.TestSupport;

namespace VendorRisk.UnitTests.Vendors;

/// <summary>
/// Certificates are catalogue rows joined to the vendor many-to-many, so the behaviour the rules
/// and the API contract depend on now lives on the link collection rather than on a string array.
/// </summary>
public class VendorCertificateTests
{
    private static SecurityCertificate Certificate(string code, int id = 0) =>
        new() { Id = id, Code = code, Name = code };

    [Fact]
    public void SetCertificates_replaces_the_previous_links()
    {
        var vendor = new VendorBuilder().WithCerts("ISO27001").Build();

        vendor.SetCertificates([Certificate("SOC2", 2)]);

        Assert.Equal(["SOC2"], vendor.SecurityCerts);
        Assert.False(vendor.HasCertification(RiskThresholds.RequiredCertification));
    }

    [Fact]
    public void SetCertificates_drops_repeats_of_the_same_code()
    {
        var vendor = VendorBuilder.Clean();

        // The catalogue is unique by code, but a caller can still hand the same row over twice;
        // the join table's composite key would reject the duplicate pair.
        vendor.SetCertificates([Certificate("ISO27001", 1), Certificate("iso27001", 1), Certificate("SOC2", 2)]);

        Assert.Equal(2, vendor.Certificates.Count);
        Assert.Equal(["ISO27001", "SOC2"], vendor.SecurityCerts);
    }

    [Fact]
    public void SetCertificates_treats_null_as_no_certificates()
    {
        var vendor = new VendorBuilder().WithCerts("ISO27001").Build();

        vendor.SetCertificates(null);

        Assert.Empty(vendor.SecurityCerts);
    }

    [Fact]
    public void SecurityCerts_is_sorted_so_the_payload_does_not_depend_on_join_order()
    {
        var vendor = VendorBuilder.Clean();

        vendor.SetCertificates([Certificate("SOC2", 2), Certificate("ISO27001", 1), Certificate("PCI-DSS", 3)]);

        Assert.Equal(["ISO27001", "PCI-DSS", "SOC2"], vendor.SecurityCerts);
    }

    [Fact]
    public void HasCertification_ignores_case()
    {
        var vendor = new VendorBuilder().WithCerts("iso27001").Build();

        Assert.True(vendor.HasCertification("ISO27001"));
        Assert.Null(new MissingIso27001Rule().Evaluate(vendor));
    }

    /// <summary>
    /// The seeder registers any code the catalogue does not describe, but such a code would carry
    /// no proper name - so the two shipped datasets must agree.
    /// </summary>
    [Fact]
    public void Every_certificate_the_sample_vendors_hold_is_described_by_the_catalogue()
    {
        var catalog = Read<SecurityCertificateDataFile>("SecurityCertificates.json");
        var vendors = Read<SampleVendorDataFile>("SampleVendorData.json");

        var described = catalog.Certificates
            .Select(certificate => certificate.Code)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var used = vendors.Vendors
            .SelectMany(vendor => SecurityCertificates.Normalise(vendor.SecurityCerts))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.Empty(used.Except(described, StringComparer.OrdinalIgnoreCase));
    }

    [Fact]
    public void The_catalogue_holds_no_duplicate_codes()
    {
        var codes = Read<SecurityCertificateDataFile>("SecurityCertificates.json")
            .Certificates
            .Select(certificate => certificate.Code)
            .ToList();

        Assert.Equal(codes.Count, codes.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    private static T Read<T>(string fileName)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "data", fileName);
        var value = JsonSerializer.Deserialize<T>(
            File.ReadAllText(path),
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.NotNull(value);

        return value;
    }
}
