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

    // Add OpenAPI services with enhanced documentation (T125)
    builder.Services.AddOpenApi(options =>
    {
        options.AddDocumentTransformer((document, context, cancellationToken) =>
        {
            document.Info = new()
            {
                Title = "MALIEV Currency Service API",
                Version = "v1",
                Description = """
                    ## Overview
                    Currency WebAPI Service providing exchange rates, currency metadata, and snapshot management.

                    ## Features
                    - **Currency Metadata**: Query currencies by code, country, or list all
                    - **Live Exchange Rates**: Real-time rates with provider failover and transitive conversion
                    - **Snapshot Queries**: Historical exchange rates for accounting and audit
                    - **Batch Ingestion**: Admin endpoints for bulk snapshot uploads
                    - **Currency Management**: Admin CRUD operations with optimistic concurrency

                    ## Authentication
                    Admin endpoints require JWT Bearer token with `Admin` role.
                    Include in request header: `Authorization: Bearer <your-jwt-token>`

                    ## Rate Limiting
                    Public endpoints: 100 requests/minute per API key
                    Admin endpoints: 50 requests/minute per user

                    ## Caching
                    - Currency metadata: 5 minutes TTL
                    - Live rates: 5 minutes TTL (extended to 60 minutes on provider failure)
                    - Snapshot rates: 60 minutes TTL (immutable data)

                    ## Provider Failover
                    1. Fawazahmed API (primary)
                    2. Frankfurter API (fallback)
                    3. Stale cache (last resort)
                    """,
                Contact = new()
                {
                    Name = "MALIEV Support",
                    Email = "support@maliev.com",
                    Url = new("https://github.com/MALIEV-Co-Ltd/Maliev.CurrencyService")
                },
                License = new()
                {
                    Name = "Proprietary",
                    Url = new("https://maliev.com/license")
                }
            };

            // Add server information
            document.Servers = new[]
            {
                new Microsoft.OpenApi.Models.OpenApiServer
                {
                    Url = "http://localhost:5000",
                    Description = "Development"
                },
                new Microsoft.OpenApi.Models.OpenApiServer
                {
                    Url = "https://staging.api.maliev.com/currencies",
                    Description = "Staging"
                },
                new Microsoft.OpenApi.Models.OpenApiServer
                {
                    Url = "https://api.maliev.com/currencies",
                    Description = "Production"
                }
            };

            // Add security scheme
            document.Components ??= new();
            document.Components.SecuritySchemes = new Dictionary<string, Microsoft.OpenApi.Models.OpenApiSecurityScheme>
            {
                ["Bearer"] = new()
                {
                    Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
                    Scheme = "bearer",
                    BearerFormat = "JWT",
                    Description = "JWT Authorization header using the Bearer scheme. Example: \"Authorization: Bearer {token}\""
                }
            };

            return Task.CompletedTask;
        });
    });

    // Configure Currency Service DbContext with snake_case naming
    // Constitution Principle IV: Always use PostgreSQL (no InMemoryDatabase)
    builder.Services.AddDbContext<CurrencyServiceDbContext>(options =>
    {
        // Use ConnectionStrings__CurrencyDbContext from Google Secret Manager
        var connectionString = builder.Configuration.GetConnectionString("CurrencyDbContext")
            ?? Environment.GetEnvironmentVariable("ServiceDbContext");

        options.UseNpgsql(connectionString)
            .UseSnakeCaseNamingConvention(); // Apply snake_case naming per data-model.md

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