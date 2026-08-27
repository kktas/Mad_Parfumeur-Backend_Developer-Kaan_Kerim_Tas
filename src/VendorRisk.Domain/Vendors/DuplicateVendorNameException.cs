namespace VendorRisk.Domain.Vendors;

/// <summary>
/// Raised when a vendor name is already taken. Vendor names are unique irrespective of case, so
/// "TechPlus Solutions" and "techplus solutions" are the same name.
/// </summary>
public sealed class DuplicateVendorNameException : Exception
{
    public DuplicateVendorNameException(string vendorName)
        : base($"A vendor named '{vendorName}' already exists.")
    {
        VendorName = vendorName;
    }

    public string VendorName { get; }
}
