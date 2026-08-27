using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using VendorRisk.Application.Abstractions;
using VendorRisk.Domain.Risk;
using VendorRisk.Infrastructure.Caching;
using VendorRisk.Infrastructure.Persistence;
using VendorRisk.Infrastructure.Scoring;
using VendorRisk.Infrastructure.Seeding;

namespace VendorRisk.Infrastructure.DependencyInjection;

/// <summary>
/// Registers PostgreSQL persistence, the unit of work, the risk factor matrix, the cache and the
/// seeder.
/// </summary>
public static class InfrastructureServiceCollectionExtensions
{
    public const string PostgresConnectionName = "Postgres";
    public const string RedisConnectionName = "Redis";

    /// <param name="contentRootPath">
    /// Base for the shipped data files; see <see cref="DataPaths"/>.
    /// </param>
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        string contentRootPath)
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

        // The matrix is read-only reference data, so it is read from disk once and shared.
        services.AddSingleton<IRiskFactorMatrix>(provider => JsonRiskFactorMatrix.Load(
            DataPaths.Resolve(
                configuration, DataPaths.RiskFactorMatrixKey, DataPaths.RiskFactorMatrixDefault, contentRootPath),
            provider.GetRequiredService<ILogger<JsonRiskFactorMatrix>>()));
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
