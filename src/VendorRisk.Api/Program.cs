using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Serilog;
using VendorRisk.Api.Middleware;
using VendorRisk.Application.DependencyInjection;
using VendorRisk.Infrastructure.DependencyInjection;
using VendorRisk.Infrastructure.Persistence;
using VendorRisk.Infrastructure.Seeding;

var builder = WebApplication.CreateBuilder(args);

// Serilog is configured entirely from appsettings so the ELK sink can be switched on per
// environment without a rebuild. See README > Logging.
builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext()
    .Enrich.WithMachineName());

builder.Services
    .AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new()
    {
        Title = "Vendor Risk Scoring Engine",
        Version = "v1",
        Description =
            "Rule-based vendor risk scoring. riskScore is always 0 in this build; " +
            "riskLevel and reason are computed from the case study section 5 rules."
    });

    var xmlPath = Path.Combine(AppContext.BaseDirectory, $"{typeof(Program).Assembly.GetName().Name}.xml");
    if (File.Exists(xmlPath))
    {
        options.IncludeXmlComments(xmlPath);
    }
});

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

var healthChecks = builder.Services.AddHealthChecks();

var postgresConnectionString = builder.Configuration.GetConnectionString(
    InfrastructureServiceCollectionExtensions.PostgresConnectionName);
if (!string.IsNullOrWhiteSpace(postgresConnectionString))
{
    healthChecks.AddNpgSql(postgresConnectionString, name: "postgres");
}

var redisConnectionString = builder.Configuration.GetConnectionString(
    InfrastructureServiceCollectionExtensions.RedisConnectionName);
if (!string.IsNullOrWhiteSpace(redisConnectionString))
{
    healthChecks.AddRedis(redisConnectionString, name: "redis");
}

var app = builder.Build();

app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseSerilogRequestLogging();

if (app.Configuration.GetValue("Database:MigrateOnStartup", true))
{
    await MigrateAndSeedAsync(app);
}

app.UseSwagger();
app.UseSwaggerUI(options => options.SwaggerEndpoint("/swagger/v1/swagger.json", "Vendor Risk Scoring Engine v1"));

// Serves the comparison dashboard from wwwroot/index.html.
app.UseDefaultFiles();
app.UseStaticFiles();

app.MapControllers();
app.MapHealthChecks("/health");

app.Run();

// Applies pending migrations and seeds the sample dataset. Disable with Database:MigrateOnStartup
// when migrations are applied out of band, e.g. by a deployment pipeline.
static async Task MigrateAndSeedAsync(WebApplication app)
{
    using var scope = app.Services.CreateScope();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    try
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<VendorRiskDbContext>();
        await dbContext.Database.MigrateAsync();

        var contentRoot = app.Environment.ContentRootPath;
        var datasetPath = ResolveDataPath(app.Configuration, "Database:SeedDatasetPath", "data/SampleVendorData.json", contentRoot);
        var catalogPath = ResolveDataPath(app.Configuration, "Database:SeedCertificateCatalogPath", "data/SecurityCertificates.json", contentRoot);

        await scope.ServiceProvider.GetRequiredService<DataSeeder>().SeedAsync(datasetPath, catalogPath);
    }
    catch (Exception ex)
    {
        // Fail loudly: an API that starts without its schema only fails later, per request.
        logger.LogCritical(ex, "Database migration or seeding failed");
        throw;
    }
}

static string ResolveDataPath(IConfiguration configuration, string key, string fallback, string contentRootPath)
{
    var configured = configuration[key] ?? fallback;

    return Path.IsPathRooted(configured) ? configured : Path.Combine(contentRootPath, configured);
}

/// <summary>Exposed so the integration-style tests can reference the API's entry point assembly.</summary>
public partial class Program;
