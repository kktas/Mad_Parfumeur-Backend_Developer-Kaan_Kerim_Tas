namespace VendorRisk.Application.Abstractions;

/// <summary>Cache key layout, kept in one place so reads and invalidations cannot diverge.</summary>
public static class CacheKeys
{
    public static string Assessment(int vendorId) => $"vendor:{vendorId}:assessment";
}
