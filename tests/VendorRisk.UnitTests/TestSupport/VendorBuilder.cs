using VendorRisk.Domain.Vendors;

namespace VendorRisk.UnitTests.TestSupport;

/// <summary>
/// Builds vendors for tests. Defaults are deliberately clean - a vendor that trips no rule at all -
/// so each test only states the fields it cares about.
/// </summary>
public sealed class VendorBuilder
{
    private int _id = 1;
    private string _name = "Test Vendor";
    private int _financialHealth = 70;
    private decimal _slaUptime = 99m;
    private int _majorIncidents = 0;
    private List<string> _securityCerts = ["ISO27001"];
    private bool _contractValid = true;
    private bool _privacyPolicyValid = true;
    private bool _pentestReportValid = true;

    public VendorBuilder WithId(int id)
    {
        _id = id;
        return this;
    }

    public VendorBuilder WithName(string name)
    {
        _name = name;
        return this;
    }

    public VendorBuilder WithFinancialHealth(int financialHealth)
    {
        _financialHealth = financialHealth;
        return this;
    }

    public VendorBuilder WithSlaUptime(decimal slaUptime)
    {
        _slaUptime = slaUptime;
        return this;
    }

    public VendorBuilder WithMajorIncidents(int majorIncidents)
    {
        _majorIncidents = majorIncidents;
        return this;
    }

    public VendorBuilder WithCerts(params string[] certs)
    {
        _securityCerts = [.. certs];
        return this;
    }

    public VendorBuilder WithDocuments(bool contractValid = true, bool privacyPolicyValid = true, bool pentestReportValid = true)
    {
        _contractValid = contractValid;
        _privacyPolicyValid = privacyPolicyValid;
        _pentestReportValid = pentestReportValid;
        return this;
    }

    public VendorProfile Build() => new()
    {
        Id = _id,
        Name = _name,
        FinancialHealth = _financialHealth,
        SlaUptime = _slaUptime,
        MajorIncidents = _majorIncidents,
        // Catalogue rows as the repository would hand them over. Codes are kept exactly as the
        // test supplied them, so case-insensitive matching stays under test.
        Certificates =
        [
            .. _securityCerts.Select((code, index) => new SecurityCertificate
            {
                Id = index + 1,
                Code = code,
                Name = code
            })
        ],
        Documents = new VendorDocuments
        {
            ContractValid = _contractValid,
            PrivacyPolicyValid = _privacyPolicyValid,
            PentestReportValid = _pentestReportValid
        },
        CreatedAtUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        UpdatedAtUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
    };

    /// <summary>A vendor that trips no rule: healthy finances within band, full SLA, all documents valid.</summary>
    public static VendorProfile Clean() => new VendorBuilder().Build();
}
