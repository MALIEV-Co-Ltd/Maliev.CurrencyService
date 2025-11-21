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
    });

    // Configure caching (simple configuration per research.md decision 3 CRITICAL section)
    builder.Services.AddMemoryCache(); // Simple config without SizeLimit to avoid runtime exceptions

    // Configure Redis distributed cache (T031)
    builder.Services.Configure<RedisOptions>(builder.Configuration.GetSection(RedisOptions.SectionName));
    var redisSection = builder.Configuration.GetSection(RedisOptions.SectionName);
    if (redisSection.Exists())
    {
        var redisOptions = new RedisOptions { ConnectionString = "default" };
        redisSection.Bind(redisOptions);

        // Register IConnectionMultiplexer for Redis (used by RedisHealthCheck and caching)
        builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
        {
            var configuration = ConfigurationOptions.Parse(redisOptions.ConnectionString);
            configuration.ConnectTimeout = redisOptions.ConnectTimeout * 1000;
            configuration.SyncTimeout = redisOptions.SyncTimeout;
            configuration.AbortOnConnectFail = redisOptions.AbortOnConnectFail;
            configuration.DefaultDatabase = redisOptions.Database;

            return ConnectionMultiplexer.Connect(configuration);
        });

        // Register IDistributedCache using StackExchange.Redis
        builder.Services.AddStackExchangeRedisCache(options =>
        {
            options.Configuration = redisOptions.ConnectionString;
            options.InstanceName = redisOptions.InstanceName ?? "CurrencyService:";
        });

        Log.Information("Redis distributed cache configured at {ConnectionString}", redisOptions.ConnectionString);
    }
    else
    {
        Log.Warning("Redis not configured - service will run with in-memory cache only");
    }

    // Configure rate limiting
    builder.Services.AddRateLimiter(options =>
    {
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

        options.AddPolicy("CurrencyPolicy", context =>
            RateLimitPartition.GetSlidingWindowLimiter(
                partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                factory: _ => new SlidingWindowRateLimiterOptions
                {
                    PermitLimit = 100,
                    Window = TimeSpan.FromMinutes(1),
                    SegmentsPerWindow = 2,
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    QueueLimit = 10
                }));
    });

    // Register services
    builder.Services.AddScoped<ICurrencyService, Maliev.CurrencyService.Api.Services.CurrencyService>();
    builder.Services.AddScoped<ICacheTagService, CacheTagService>();
    builder.Services.AddScoped<IRateService, RateService>();
    builder.Services.AddScoped<ISnapshotService, SnapshotService>();

    // Register metrics singleton (Constitution Principle XI, FR-052)
    builder.Services.AddSingleton<CurrencyServiceMetrics>();

    // Register cache service (T033 - two-tier caching per research.md decision 3)
    builder.Services.AddSingleton<ICacheService, RedisCacheService>();

    // Register FluentValidation validators
    builder.Services.AddValidatorsFromAssemblyContaining<Program>();

    // Configure exchange rate options
    builder.Services.Configure<ExchangeRateOptions>(builder.Configuration.GetSection(ExchangeRateOptions.SectionName));
    builder.Services.AddOptions<ExchangeRateOptions>()
        .Bind(builder.Configuration.GetSection(ExchangeRateOptions.SectionName))
        .ValidateDataAnnotations();

    // Register exchange rate providers with typed HttpClients (T035-T036)
    builder.Services.AddHttpClient<FawazahmedProvider>((serviceProvider, httpClient) =>
    {
        var exchangeRateOptions = serviceProvider.GetRequiredService<IOptions<ExchangeRateOptions>>().Value;
        httpClient.Timeout = TimeSpan.FromSeconds(exchangeRateOptions.TimeoutSeconds);
    });
    builder.Services.AddHttpClient<FrankfurterProvider>((serviceProvider, httpClient) =>
    {
        var exchangeRateOptions = serviceProvider.GetRequiredService<IOptions<ExchangeRateOptions>>().Value;
        httpClient.Timeout = TimeSpan.FromSeconds(exchangeRateOptions.TimeoutSeconds);
    });

    // Register providers for DI (T037)
    builder.Services.AddScoped<IExchangeRateProvider, FawazahmedProvider>();
    builder.Services.AddScoped<IExchangeRateProvider, FrankfurterProvider>();
    builder.Services.AddScoped<ProviderChain>();

    // Register background services
    builder.Services.AddHostedService<Maliev.CurrencyService.Api.BackgroundServices.CacheWarmingService>();
    builder.Services.AddHostedService<Maliev.CurrencyService.Api.BackgroundServices.SnapshotCleanupService>();

    // Configure CORS
    builder.Services.AddCors(options =>
    {
        options.AddDefaultPolicy(
            policy =>
            {
                policy.WithOrigins(
                    "https://maliev.com",
                    "https://*.maliev.com",
                    "http://maliev.com",
                    "http://*.maliev.com")
                .AllowAnyHeader()
                .AllowAnyMethod();
            });
    });

    // Configure JWT Authentication (skip in Testing environment)
    if (!builder.Environment.IsEnvironment("Testing"))
    {
        var jwtSection = builder.Configuration.GetSection(JwtOptions.SectionName);
        if (jwtSection.Exists())
        {
            builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options =>
                {
                    var jwtOptions = new JwtOptions
                    {
                        Issuer = "default-issuer",
                        Audience = "default-audience",
                        PublicKey = "default-key"
                    };
                    jwtSection.Bind(jwtOptions);

                    // Use RSA public key validation from shared config (maliev-dev-shared-config)
                    var publicKeyBytes = Convert.FromBase64String(jwtOptions.PublicKey);
                    var publicKeyPem = Encoding.UTF8.GetString(publicKeyBytes);

                    // Import RSA public key from PEM format
                    var rsa = System.Security.Cryptography.RSA.Create();
                    rsa.ImportFromPem(publicKeyPem);

                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidateAudience = true,
                        ValidateLifetime = true,
                        ValidateIssuerSigningKey = true,
                        ValidIssuer = jwtOptions.Issuer,
                        ValidAudience = jwtOptions.Audience,
                        IssuerSigningKey = new RsaSecurityKey(rsa)
                    };
                });
        }
        else
        {
            // Log warning that JWT is not configured for local development
            Log.Warning("JWT configuration not found - API will start but authentication will not work. Configure JWT secrets for full functionality.");
        }
    }

    builder.Services.AddAuthorization();

    builder.Services.Configure<ForwardedHeadersOptions>(options =>
    {
        options.ForwardedHeaders =
            ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        options.KnownNetworks.Clear();
        options.KnownProxies.Clear();
    });

    builder.Services.AddHealthChecks()
        .AddCheck<DatabaseHealthCheck>("database", tags: new[] { "readiness" })
        .AddCheck<RedisHealthCheck>("cache", tags: new[] { "readiness" })
        .AddCheck("liveness", () => HealthCheckResult.Healthy(), tags: new[] { "liveness" })
        .AddPrivateMemoryHealthCheck(4_000_000_000L, "memory_private", tags: new[] { "liveness", "readiness" }) // 4 GB
        .AddWorkingSetHealthCheck(4_000_000_000L, "memory_working_set", tags: new[] { "liveness", "readiness" }); // 4 GB

    // Add service defaults for .NET Aspire
    builder.AddServiceDefaults();

    var app = builder.Build();

    app.UseForwardedHeaders();

    // Add security headers middleware early in pipeline (Constitution Principle VI)
    app.UseSecurityHeaders();

    // Add correlation ID middleware early in pipeline
    app.UseCorrelationId();

    // Add custom metrics middleware for HTTP request tracking (FR-052)
    app.UseRequestMetrics();

    // Configure the HTTP request pipeline
    // OpenAPI specification generation using .NET 9 built-in support (used by Scalar)
    app.MapOpenApi("/currencies/openapi/{documentName}.json");
    app.MapScalarApiReference(options =>
    {
        options.WithEndpointPrefix("/currencies/scalar/{documentName}");
        options.WithOpenApiRoutePattern("/currencies/openapi/{documentName}.json");
    });

    app.UseMiddleware<ExceptionHandlingMiddleware>();
    app.UseHttpsRedirection();

    // Prometheus HTTP metrics middleware (FR-052)
    app.UseHttpMetrics();

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