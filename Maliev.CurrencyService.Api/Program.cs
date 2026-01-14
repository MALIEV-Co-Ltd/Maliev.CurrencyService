#pragma warning disable CA1848 // For improved performance, use the LoggerMessage delegates
using Maliev.Aspire.ServiceDefaults;
using Maliev.CurrencyService.Api.Metrics;
using Maliev.CurrencyService.Api.Services;
using Maliev.CurrencyService.Api.Services.External;
using Maliev.CurrencyService.Data;
using Maliev.CurrencyService.Api.BackgroundServices;
using Microsoft.Extensions.Logging;
using System.Threading.RateLimiting;

// Initialize bootstrap logging
using var loggerFactory = LoggerFactory.Create(logBuilder => logBuilder.AddConsole());
var bootstrapLogger = loggerFactory.CreateLogger("Program");

try
{
    bootstrapLogger.LogInformation("Starting Currency Service host");

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

    // Add Cache Service (standardized via ServiceDefaults)
    builder.AddRedisDistributedCache(instanceName: "currency:");
    builder.Services.AddMemoryCache();

    // MassTransit with RabbitMQ
    builder.AddMassTransitWithRabbitMq();

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

    // Add Rate Limiting
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

    // IAM Registration
    builder.AddIAMServiceClient("currency");
    builder.Services.AddIAMRegistration<CurrencyIAMRegistrationService>("currency");

    builder.Services.AddControllers()
        .ConfigureApiBehaviorOptions(options =>
        {
            options.SuppressModelStateInvalidFilter = true;
        });

    var app = builder.Build();

    // Force instantiation of metrics service to ensure OpenTelemetry meters are created
    var metricsService = app.Services.GetRequiredService<CurrencyServiceMetrics>();

    var logger = app.Services.GetRequiredService<ILogger<Program>>();

    // Run database migrations on startup
    await app.MigrateDatabaseAsync<CurrencyDbContext>();

    app.UseStandardMiddleware();
    if (!app.Environment.IsDevelopment())
    {
        app.UseHttpsRedirection();
    }
    app.UseRateLimiter();
    app.UseCors();

    // JWT Authentication & Authorization
    app.UseAuthentication();
    app.UseAuthorization();

    // Map endpoints after middleware
    app.MapControllers();

    // Map Aspire default endpoints (/health, /alive, /metrics)
    app.MapDefaultEndpoints(servicePrefix: "currency");

    // Map OpenAPI and Scalar documentation (dev/staging only)
    app.MapApiDocumentation(servicePrefix: "currency");

    logger.LogInformation("CurrencyService started successfully");
    await app.RunAsync();
}
catch (Exception ex)
{
    bootstrapLogger.LogCritical(ex, "Currency Service host terminated unexpectedly during startup");
    throw;
}
finally
{
    loggerFactory.Dispose();
}

/// <summary>
/// Main entry point for the Maliev Currency Service API.
/// </summary>
public partial class Program { }
