using System.Text.Json.Serialization;

namespace VendorRisk.Infrastructure.Seeding;

/// <summary>
/// Shape of data/SecurityCertificates.json, the certificate catalogue. The case study names the
/// certifications (section 2) but ships no catalogue of its own, so this file supplies the codes
/// used by the sample vendors along with their full names.
/// </summary>
public sealed class SecurityCertificateDataFile
{
    [JsonPropertyName("Certificates")]
    public List<SecurityCertificateRecord> Certificates { get; set; } = [];
}

public sealed class SecurityCertificateRecord
{
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }
}
