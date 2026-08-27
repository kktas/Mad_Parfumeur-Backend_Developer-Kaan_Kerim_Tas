using VendorRisk.Application.Abstractions;

namespace VendorRisk.Infrastructure.Caching;

/// <summary>
/// Registered when no Redis connection string is configured, so the API runs without Redis and
/// every assessment read is simply recomputed.
/// </summary>
public sealed class NullCacheService : ICacheService
{
    public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) where T : class =>
        Task.FromResult<T?>(null);

    public Task SetAsync<T>(string key, T value, TimeSpan? ttl = null, CancellationToken cancellationToken = default) where T : class =>
        Task.CompletedTask;

    public Task RemoveAsync(string key, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}
