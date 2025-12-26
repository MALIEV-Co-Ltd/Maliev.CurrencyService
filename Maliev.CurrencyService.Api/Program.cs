
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
builder.AddStandardMiddleware(options =>
{
    options.EnableRequestLogging = true;
});
builder.AddServiceMeters("currencies-meter"); // Register service meters for OpenTelemetry business metrics

builder.AddPostgresDbContext<CurrencyDbContext>(connectionName: "CurrencyDbContext"); // PostgreSQL with retry logic

// Add Cache Service (two-tier with Redis or in-memory only)
// ServiceDefaults 'AddRedisDistributedCache' handles:
// 1. Connection string 'redis'
// 2. AddStackExchangeRedisCache
// 3. Health Checks (tags: redis, ready)
// 4. In-Memory fallback (AddMemoryCache)
builder.AddRedisDistributedCache(instanceName: "currency:");
builder.Services.AddMemoryCache();

builder.Services.AddSingleton<ICacheService, RedisCacheService>();
// --- API Configuration ---
builder.AddDefaultCors(); // CORS from CORS:AllowedOrigins config
builder.AddDefaultApiVersioning(); // API versioning with URL segment reader

// JWT Authentication (tests override via PostConfigureAll with dynamic RSA keys)
builder.AddJwtAuthentication();

// Add OpenAPI (must be in Program.cs for XML comments to work via source generator)
if (!builder.Environment.IsProduction())
{
    builder.AddStandardOpenApi(
        title: "MALIEV Currency Service API",
        description: "Currency and exchange rate service. Provides ISO 4217 currency metadata, country-to-currency resolution, exchange rate snapshots with historical tracking, and administrative endpoints for currency management with optimistic concurrency control.");
}



// Add Rate Limiting (disabled in Testing environment to support concurrent test scenarios)
if (!builder.Environment.IsEnvironment("Testing"))
{
    builder.Services.AddRateLimiter(options =>
    {
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

        // Policy for anonymous users (IP-based)
        options.AddPolicy("PublicApi", httpContext =>
            RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
                factory: _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 100,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 20
                }));

        // Policy for authenticated users (Identity-based)
        options.AddPolicy("AuthenticatedApi", httpContext =>
            RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: httpContext.User.Identity?.Name ?? "anonymous",
                factory: _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 1000,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 50
                }));
    });
}

// Add Metrics
// Add Metrics
builder.Services.AddSingleton<CurrencyServiceMetrics>();
// Register IDatabaseMetrics to resolve to the same CurrencyServiceMetrics instance
builder.Services.AddSingleton<Maliev.CurrencyService.Data.Interceptors.IDatabaseMetrics>(
    sp => sp.GetRequiredService<CurrencyServiceMetrics>());

// Add Data Interceptors (skip in Testing environment to allow simpler DbContext constructor)
if (!builder.Environment.IsEnvironment("Testing"))
{
    builder.Services.AddScoped<Maliev.CurrencyService.Data.Interceptors.DatabaseMetricsInterceptor>();
    builder.Services.AddScoped<Maliev.CurrencyService.Data.Interceptors.AuditLogInterceptor>();
}

// Add Domain Services
builder.Services.AddScoped<ICurrencyService, CurrencyService>();
builder.Services.AddScoped<ISnapshotService, SnapshotService>();
builder.Services.AddScoped<IRateService, RateService>();

// Add External Providers
builder.Services.AddHttpClient<FawazahmedProvider>().AddStandardResilienceHandler();
builder.Services.AddHttpClient<FrankfurterProvider>().AddStandardResilienceHandler();

// IAM Service Client
builder.AddServiceClient("IAMService");

// Register them as IExchangeRateProvider by resolving the typed client
builder.Services.AddScoped<IExchangeRateProvider>(sp => sp.GetRequiredService<FawazahmedProvider>());
builder.Services.AddScoped<IExchangeRateProvider>(sp => sp.GetRequiredService<FrankfurterProvider>());

builder.Services.AddScoped<ProviderChain>();

builder.Services.AddSingleton<ISnapshotQueue, SnapshotQueue>();
builder.Services.AddHostedService<SnapshotProcessingService>();
builder.Services.AddIAMRegistration<CurrencyIAMRegistrationService>();

builder.Services.AddControllers();

var app = builder.Build();

// Force instantiation of metrics service to ensure OpenTelemetry meters are created
var metricsService = app.Services.GetRequiredService<CurrencyServiceMetrics>();

var logger = app.Services.GetRequiredService<ILogger<Program>>();

// Run database migrations on startup
try
{
    await app.MigrateDatabaseAsync<CurrencyDbContext>();
}
catch (Exception ex)
{
    logger.LogError(ex, "Database migration failed - application may not function correctly");
    // Don't throw - allow app to start for debugging
}

app.UseStandardMiddleware();

app.UseHttpsRedirection();
if (!app.Environment.IsEnvironment("Testing"))
{
    app.UseRateLimiter();
}
app.UseCors();

// JWT Authentication & Authorization
app.UseAuthentication();
app.UseAuthorization();

// Map endpoints after middleware
app.MapControllers();

// Map Aspire default endpoints (/health, /alive, /metrics)
// Standardized pattern used throughout MALIEV's services
app.MapDefaultEndpoints(servicePrefix: "currency");

// Map OpenAPI and Scalar documentation (dev/staging only)
app.MapApiDocumentation(servicePrefix: "currency");

logger.LogInformation("CurrencyService started successfully");
await app.RunAsync();

/// <summary>
/// Main entry point for the Maliev Currency Service API.
/// </summary>
public partial class Program { }
