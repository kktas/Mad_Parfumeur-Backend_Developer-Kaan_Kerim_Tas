using VendorRisk.Domain.Rules;
using VendorRisk.Domain.Vendors;
using VendorRisk.UnitTests.TestSupport;

namespace VendorRisk.UnitTests.Rules;

public class SecurityCertificatesTests
{
    [Theory]
    [InlineData("iso27001", "ISO27001")]
    [InlineData("Iso27001", "ISO27001")]
    [InlineData("  soc2  ", "SOC2")]
    [InlineData("pci-dss", "PCI-DSS")]
    public void Normalise_trims_and_upper_cases(string input, string expected)
    {
        Assert.Equal([expected], SecurityCertificates.Normalise([input]));
    }

    [Fact]
    public void Normalise_collapses_case_insensitive_duplicates()
    {
        var result = SecurityCertificates.Normalise(["ISO27001", "iso27001", " Iso27001 ", "SOC2"]);

        Assert.Equal(["ISO27001", "SOC2"], result);
    }

    [Fact]
    public void Normalise_drops_blank_entries()
    {
        Assert.Equal(["ISO27001"], SecurityCertificates.Normalise(["ISO27001", "", "   ", "\t"]));
    }

    [Fact]
    public void Normalise_preserves_the_order_first_seen()
    {
        var result = SecurityCertificates.Normalise(["soc2", "iso27001", "SOC2", "pci-dss"]);

        Assert.Equal(["SOC2", "ISO27001", "PCI-DSS"], result);
    }

    [Fact]
    public void Normalise_handles_null_and_empty_input()
    {
        Assert.Empty(SecurityCertificates.Normalise(null));
        Assert.Empty(SecurityCertificates.Normalise([]));
    }

    /// <summary>
    /// The required certificate constant is already upper-cased, so a normalised list matches it
    /// directly. HasCertification stays case-insensitive as a guard for any rows written before
    /// normalisation existed.
    /// </summary>
    [Fact]
    public void Normalised_certificates_satisfy_the_ISO27001_rule()
    {
        var vendor = new VendorBuilder().WithCerts("iso27001").Build();
        vendor.SetCertificates(SecurityCertificates
            .Normalise(vendor.SecurityCerts)
            .Select(code => new SecurityCertificate { Code = code, Name = code }));

        Assert.Equal(RiskThresholds.RequiredCertification, vendor.SecurityCerts.Single());
        Assert.True(vendor.HasCertification(RiskThresholds.RequiredCertification));
        Assert.Null(new MissingIso27001Rule().Evaluate(vendor));
    }
}
