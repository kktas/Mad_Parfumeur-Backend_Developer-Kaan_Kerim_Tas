using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using VendorRisk.Application.Abstractions;
using VendorRisk.Infrastructure.Caching;
using VendorRisk.Infrastructure.Persistence;
using VendorRisk.Infrastructure.Seeding;

namespace VendorRisk.Infrastructure.DependencyInjection;

/// <summary>Registers PostgreSQL persistence, the unit of work, the cache, and the seeder.</summary>
public static class InfrastructureServiceCollectionExtensions
{
    public const string PostgresConnectionName = "Postgres";
    public const string RedisConnectionName = "Redis";

    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var postgresConnectionString = configuration.GetConnectionString(PostgresConnectionName)
            ?? throw new InvalidOperationException(
                $"Connection string '{PostgresConnectionName}' is not configured. See README > Configuration.");

        services.AddDbContext<VendorRiskDbContext>(options => options.UseNpgsql(postgresConnectionString));

        services.AddScoped<IVendorRepository, VendorRepository>();
        services.AddScoped<ISecurityCertificateRepository, SecurityCertificateRepository>();

        // Scoped alongside the repositories so all three share one DbContext, which is what makes
        // a single SaveChanges cover everything an operation staged.
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<DataSeeder>();

        return services.AddCache(configuration);
    }

    /// <summary>
    /// Uses Redis when a connection string is present and falls back to a no-op cache otherwise,
    /// so the API and its tests run without a Redis instance.
    /// </summary>
    private static IServiceCollection AddCache(this IServiceCollection services, IConfiguration configuration)
    {
        var redisConnectionString = configuration.GetConnectionString(RedisConnectionName);

        if (string.IsNullOrWhiteSpace(redisConnectionString))
        {
            services.AddSingleton<ICacheService, NullCacheService>();
            return services;
        }

        services.AddStackExchangeRedisCache(options =>
        {
            options.Configuration = redisConnectionString;
            options.InstanceName = "vendorrisk:";
        });

        services.AddScoped<ICacheService, RedisCacheService>();

        return services;
    }
}
