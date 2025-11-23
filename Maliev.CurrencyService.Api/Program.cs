using Asp.Versioning;
using FluentValidation;
using Maliev.CurrencyService.Api.HealthChecks;
using Maliev.CurrencyService.Api.Metrics;
using Maliev.CurrencyService.Api.Middleware;
using Maliev.CurrencyService.Api.Models;
using Maliev.CurrencyService.Api.Services;
using Maliev.CurrencyService.Api.Services.External;
using Maliev.CurrencyService.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Prometheus;
using Scalar.AspNetCore;
using Serilog;
using StackExchange.Redis;
using System.Text;
using System.Threading.RateLimiting;
using NetEscapades.Configuration.Yaml;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .CreateLogger();

builder.Host.UseSerilog();

try
{
    Log.Information("Starting Maliev Currency Service");

    // Load secrets.yaml with configurable path
    var secretsPath = builder.Configuration.GetValue<string>("Secrets:Path", "secrets.yaml");
    builder.Configuration.AddYamlFile(secretsPath, optional: true, reloadOnChange: true);

    // Load secrets from mounted Kubernetes secrets
    var kubernetesSecretsPath = builder.Configuration.GetValue<string>("Secrets:KubernetesPath", "/mnt/secrets");
    if (Directory.Exists(kubernetesSecretsPath))
    {
        builder.Configuration.AddKeyPerFile(directoryPath: kubernetesSecretsPath, optional: true);
    }

    // API Versioning
    builder.Services.AddApiVersioning(options =>
    {
        options.DefaultApiVersion = new ApiVersion(1, 0);
        options.AssumeDefaultVersionWhenUnspecified = true;
        options.ReportApiVersions = true;
        options.ApiVersionReader = new UrlSegmentApiVersionReader();
    }).AddApiExplorer(options =>
    {
        options.GroupNameFormat = "'v'VVV";
        options.SubstituteApiVersionInUrl = true;
    });

    // Add controllers
    builder.Services.AddControllers();
    builder.Services.AddEndpointsApiExplorer();
    
    // Add FluentValidation validators
    builder.Services.AddValidatorsFromAssemblyContaining<Program>();

    // Add Health Checks
    builder.Services.AddHealthChecks()
        .AddCheck<DatabaseHealthCheck>("database", tags: new[] { "readiness" })
        .AddCheck<RedisHealthCheck>("redis", tags: new[] { "readiness" })
        .AddCheck("self", () => HealthCheckResult.Healthy(), tags: new[] { "liveness" });

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

    // Add OpenAPI services
    builder.Services.AddOpenApi();

    // Add Metrics
    builder.Services.AddSingleton<CurrencyServiceMetrics>();

    // Add Cache Service
    var redisConnectionString = builder.Configuration.GetConnectionString("Redis");
    if (!string.IsNullOrEmpty(redisConnectionString))
    {
        builder.Services.AddStackExchangeRedisCache(options =>
        {
            options.Configuration = redisConnectionString;
        });
        builder.Services.AddSingleton<ICacheService, RedisCacheService>();
    }
    else
    {
        builder.Services.AddMemoryCache();
        builder.Services.AddSingleton<ICacheService, InMemoryCacheService>();
    }

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

    // Configure Currency Service DbContext with snake_case naming
    // Constitution Principle IV: Always use PostgreSQL (no InMemoryDatabase)
    builder.Services.AddDbContext<CurrencyServiceDbContext>(options =>
    {
        // Use ConnectionStrings__CurrencyDbContext from Google Secret Manager
        var connectionString = builder.Configuration.GetConnectionString("CurrencyDbContext")
            ?? Environment.GetEnvironmentVariable("ServiceDbContext");

        options.UseNpgsql(connectionString)
            .UseSnakeCaseNamingConvention(); // Apply snake_case naming per data-model.md
    });

    var app = builder.Build();

    // Configure the HTTP request pipeline.
    if (!app.Environment.IsProduction())
    {
        app.MapOpenApi();
        app.MapScalarApiReference();
    }

    app.UseSerilogRequestLogging();

    app.UseRateLimiter();
    app.UseCors();

    // JWT Authentication & Authorization (only if configured and not in Testing environment)
    if (!app.Environment.IsEnvironment("Testing"))
    {
        var jwtSection = app.Configuration.GetSection(JwtOptions.SectionName);
        if (jwtSection.Exists())
        {
            app.UseAuthentication();
            app.UseAuthorization();
        }
    }

    app.MapControllers();

    // Health check endpoints
    app.MapHealthChecks("/currencies/liveness", new HealthCheckOptions
    {
        Predicate = healthCheck => healthCheck.Tags.Contains("liveness"),
        ResponseWriter = Maliev.CurrencyService.Api.HealthChecks.HealthCheckResponseWriter.WriteResponse
    });

    app.MapHealthChecks("/currencies/readiness", new HealthCheckOptions
    {
        Predicate = healthCheck => healthCheck.Tags.Contains("readiness"),
        ResponseWriter = Maliev.CurrencyService.Api.HealthChecks.HealthCheckResponseWriter.WriteResponse
    });

    // Prometheus metrics endpoint (accessible at /currencies/metrics per FR-052)
    app.MapMetrics("/currencies/metrics");
    app.MapMetrics("/metrics"); // Also expose at /metrics for standard convention

    // Ensure database is created and seeded
    using (var scope = app.Services.CreateScope())
    {
        var context = scope.ServiceProvider.GetRequiredService<CurrencyServiceDbContext>();
        try
        {
            if (context.Database.IsRelational())
            {
                context.Database.Migrate();
            }
            else
            {
                context.Database.EnsureCreated();
            }
            Log.Information("Database initialization completed");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "An error occurred while initializing the database");
            Console.WriteLine($"Database initialization error: {ex}");
        }
    }

    Log.Information("Maliev Currency Service started successfully");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}

public class JwtOptions
{
    public const string SectionName = "Jwt";

    public required string Issuer { get; set; }
    public required string Audience { get; set; }
    public required string PublicKey { get; set; } // Base64-encoded RSA public key from shared config
}
public partial class Program { }
