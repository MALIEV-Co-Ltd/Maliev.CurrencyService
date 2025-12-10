
using Maliev.CurrencyService.Api.Metrics;
using Maliev.CurrencyService.Api.Services;
using Maliev.CurrencyService.Api.Services.External;
using Maliev.CurrencyService.Data;
using Maliev.CurrencyService.Api.BackgroundServices;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// --- Secrets & Configuration ---
builder.AddGoogleSecretManagerVolume(); // Load secrets from /mnt/secrets if available

// --- Infrastructure & Observability ---
builder.AddServiceDefaults(); // OpenTelemetry, health checks, resilience
builder.AddServiceMeters("currency-service"); // Register service meters for OpenTelemetry business metrics

builder.AddPostgresDbContext<CurrencyServiceDbContext>(connectionStringName: "CurrencyDbContext"); // PostgreSQL with retry logic

// Add Cache Service (two-tier with Redis or in-memory only)
// ServiceDefaults 'AddRedisDistributedCache' handles:
// 1. Connection string 'redis'
// 2. AddStackExchangeRedisCache
// 3. Health Checks (tags: redis, ready)
// 4. In-Memory fallback (AddMemoryCache)
builder.AddRedisDistributedCache("currencies-cache");

var redisConnectionString = builder.Configuration.GetConnectionString("Redis");
if (!string.IsNullOrEmpty(redisConnectionString) && !builder.Environment.IsEnvironment("Testing"))
{
    builder.Services.AddSingleton<ICacheService, RedisCacheService>();
}
else
{
    builder.Services.AddSingleton<ICacheService, InMemoryCacheService>();
}

// --- API Configuration ---
builder.AddDefaultCors(); // CORS from CORS:AllowedOrigins config
builder.AddDefaultApiVersioning(); // API versioning with URL segment reader

// JWT Authentication (tests override via PostConfigureAll with dynamic RSA keys)
builder.AddJwtAuthentication();

// Add OpenAPI (must be in Program.cs for XML comments to work via source generator)
if (!builder.Environment.IsProduction())
{
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddOpenApi("v1", options =>
    {
        options.AddDocumentTransformer((document, context, cancellationToken) =>
        {
            document.Info.Title = "MALIEV Currency Service API";
            document.Info.Version = "v1";
            document.Info.Description = "Currency and exchange rate service. Provides ISO 4217 currency metadata, country-to-currency resolution, exchange rate snapshots with historical tracking, and administrative endpoints for currency management with optimistic concurrency control.";
            return Task.CompletedTask;
        });
    });
}



// Add Rate Limiting
builder.Services.AddRateLimiter(options =>
{
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.User.Identity?.Name ?? httpContext.Request.Headers.Host.ToString(),
            factory: partition => new FixedWindowRateLimiterOptions
            {
                AutoReplenishment = true,
                PermitLimit = 1000,
                Window = TimeSpan.FromMinutes(1)
            }));
});

// Add Metrics
// Add Metrics
builder.Services.AddSingleton<CurrencyServiceMetrics>();
// Register IDatabaseMetrics to resolve to the same CurrencyServiceMetrics instance
builder.Services.AddSingleton<Maliev.CurrencyService.Data.Interceptors.IDatabaseMetrics>(
    sp => sp.GetRequiredService<CurrencyServiceMetrics>());

// Add Data Interceptors
builder.Services.AddScoped<Maliev.CurrencyService.Data.Interceptors.DatabaseMetricsInterceptor>();
builder.Services.AddScoped<Maliev.CurrencyService.Data.Interceptors.AuditLogInterceptor>();

// Add Domain Services
builder.Services.AddScoped<ICurrencyService, CurrencyService>();
builder.Services.AddScoped<ISnapshotService, SnapshotService>();
builder.Services.AddScoped<IRateService, RateService>();

// Add External Providers
builder.Services.AddHttpClient<FawazahmedProvider>().AddStandardResilienceHandler();
builder.Services.AddHttpClient<FrankfurterProvider>().AddStandardResilienceHandler();

// Register them as IExchangeRateProvider by resolving the typed client
builder.Services.AddScoped<IExchangeRateProvider>(sp => sp.GetRequiredService<FawazahmedProvider>());
builder.Services.AddScoped<IExchangeRateProvider>(sp => sp.GetRequiredService<FrankfurterProvider>());

builder.Services.AddScoped<ProviderChain>();

builder.Services.AddSingleton<ISnapshotQueue, SnapshotQueue>();
builder.Services.AddHostedService<SnapshotProcessingService>();
// Enable other background services if they exist and are safe to run
// builder.Services.AddHostedService<Maliev.CurrencyService.Api.BackgroundServices.CacheWarmingService>();
// builder.Services.AddHostedService<Maliev.CurrencyService.Api.BackgroundServices.SnapshotCleanupService>();

builder.Services.AddControllers();

var app = builder.Build();

// Force instantiation of metrics service to ensure OpenTelemetry meters are created
var metricsService = app.Services.GetRequiredService<CurrencyServiceMetrics>();

var logger = app.Services.GetRequiredService<ILogger<Program>>();

// Run database migrations on startup (skip in Testing environment)
if (!app.Environment.IsEnvironment("Testing"))
{
    try
    {
        await app.MigrateDatabaseAsync<CurrencyServiceDbContext>();
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Database migration failed - application may not function correctly");
        // Don't throw - allow app to start for debugging
    }
}

// Log startup configuration
if (!string.IsNullOrEmpty(redisConnectionString))
{
    logger.LogInformation("Redis distributed cache configured");
}
else
{
    logger.LogInformation("Using in-memory cache");
}

app.UseHttpsRedirection();
app.UseRateLimiter();
app.UseCors();

// JWT Authentication & Authorization
app.UseAuthentication();
app.UseAuthorization();

// Map endpoints after middleware
app.MapControllers();

// Map Aspire default endpoints (/health, /alive, /metrics)
// Standardized pattern used throughout MALIEV's services
app.MapDefaultEndpoints(servicePrefix: "currencies");



// Map OpenAPI and Scalar documentation (dev/staging only)
app.MapApiDocumentation(servicePrefix: "currencies");

logger.LogInformation("CurrencyService started successfully");
await app.RunAsync();

/// <summary>
/// Main entry point for the Maliev Currency Service API.
/// </summary>
public partial class Program { }
