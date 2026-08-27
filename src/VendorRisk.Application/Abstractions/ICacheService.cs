namespace VendorRisk.Application.Abstractions;

/// <summary>
/// Cache boundary used for the cache-aside reads of vendor assessments. Backed by Redis when a
/// Redis connection string is configured, and by a no-op implementation otherwise, so the API
/// runs unchanged without Redis.
/// </summary>
public interface ICacheService
{
    Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) where T : class;

    Task SetAsync<T>(string key, T value, TimeSpan? ttl = null, CancellationToken cancellationToken = default) where T : class;

    Task RemoveAsync(string key, CancellationToken cancellationToken = default);
}
