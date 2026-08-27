namespace VendorRisk.Domain.Vendors;

/// <summary>
/// Raised when two requests register the same previously unknown certificate code at the same
/// moment. The row that won is in the catalogue, so repeating the request succeeds - the second
/// attempt finds the code and links to it instead of registering it again.
/// </summary>
public sealed class DuplicateCertificateCodeException : Exception
{
    public DuplicateCertificateCodeException(string code)
        : base($"The certificate code '{code}' was registered by another request. Repeat the request to link to it.")
    {
        Code = code;
    }

    public string Code { get; }
}
