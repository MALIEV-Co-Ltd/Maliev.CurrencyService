using Asp.Versioning;
using Asp.Versioning.ApiExplorer;
using HealthChecks.UI.Client;
using Maliev.CurrencyService.Api.Configurations;
using Maliev.CurrencyService.Api.HealthChecks;
using Maliev.CurrencyService.Api.Middleware;
using Maliev.CurrencyService.Api.Models;
using Maliev.CurrencyService.Api.Services;
using Maliev.CurrencyService.Data.DbContexts;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using Swashbuckle.AspNetCore.SwaggerGen;
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

    // Load secrets.yaml
    builder.Configuration.AddYamlFile("secrets.yaml", optional: true, reloadOnChange: true);

    // Load secrets from mounted Kubernetes secrets
    var secretsPath = "/mnt/secrets";
    if (Directory.Exists(secretsPath))
    {
        builder.Configuration.AddKeyPerFile(directoryPath: secretsPath, optional: true);
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

    // Configure Currency DbContext
    if (builder.Environment.IsEnvironment("Testing"))
    {
        builder.Services.AddDbContext<CurrencyDbContext>(options =>
            options.UseInMemoryDatabase("TestDb"));
    }
    else if (builder.Environment.IsDevelopment())
    {
        builder.Services.AddDbContext<CurrencyDbContext>(options =>
            options.UseInMemoryDatabase("DevelopmentDb"));
    }
    else
    {
        builder.Services.AddDbContext<CurrencyDbContext>(options =>
        {
            options.UseNpgsql(builder.Configuration.GetConnectionString("Default"));
        });
    }

    // Configure caching
    var cacheOptions = new CacheOptions();
    builder.Configuration.GetSection("Cache").Bind(cacheOptions);
    builder.Services.AddSingleton(cacheOptions);

    builder.Services.AddMemoryCache(options =>
    {
        options.SizeLimit = cacheOptions.MaxCacheSize;
    });

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

    // Configure exchange rate options
    builder.Services.Configure<ExchangeRateOptions>(builder.Configuration.GetSection(ExchangeRateOptions.SectionName));
    builder.Services.AddOptions<ExchangeRateOptions>()
        .Bind(builder.Configuration.GetSection(ExchangeRateOptions.SectionName))
        .ValidateDataAnnotations();

    // Register exchange rate providers with typed HttpClients
    builder.Services.AddHttpClient<FrankfurterProvider>();
    builder.Services.AddHttpClient<FawazahmedProvider>();
    builder.Services.AddScoped<IExchangeRateProvider, FrankfurterProvider>();
    builder.Services.AddScoped<IExchangeRateProvider, FawazahmedProvider>();

    // Register main exchange rate service
    builder.Services.AddScoped<IExchangeRateService, ExchangeRateService>();

    // Configure Swagger
    builder.Services.AddTransient<IConfigureOptions<SwaggerGenOptions>, ConfigureSwaggerOptions>();
    builder.Services.AddSwaggerGen();

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
                        SecurityKey = "default-key"
                    };
                    jwtSection.Bind(jwtOptions);

                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidateAudience = true,
                        ValidateLifetime = true,
                        ValidateIssuerSigningKey = true,
                        ValidIssuer = jwtOptions.Issuer,
                        ValidAudience = jwtOptions.Audience,
                        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SecurityKey))
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
        .AddDbContextCheck<CurrencyDbContext>("CurrencyDbContext", tags: new[] { "readiness" })
        .AddCheck<DatabaseHealthCheck>("Database Health Check", tags: new[] { "readiness" });

    var app = builder.Build();

    app.UseForwardedHeaders();

    // Add correlation ID middleware early in pipeline
    app.UseCorrelationId();

    // Configure the HTTP request pipeline
    app.UseSwagger(c => 
    {
        c.RouteTemplate = "currencies/swagger/{documentName}/swagger.json";
    });
    app.UseSwaggerUI(c =>
    {
        var provider = app.Services.GetRequiredService<IApiVersionDescriptionProvider>();
        foreach (var description in provider.ApiVersionDescriptions)
        {
            c.SwaggerEndpoint($"/currencies/swagger/{description.GroupName}/swagger.json", description.GroupName.ToUpperInvariant());
        }
        c.RoutePrefix = "currencies/swagger";
    });

    app.UseMiddleware<ExceptionHandlingMiddleware>();
    app.UseHttpsRedirection();

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
    app.MapGet("/currencies/liveness", () => "Healthy");

    app.MapHealthChecks("/currencies/readiness", new HealthCheckOptions
    {
        Predicate = healthCheck => healthCheck.Tags.Contains("readiness"),
        ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
    });

    // Ensure database is created and seeded
    using (var scope = app.Services.CreateScope())
    {
        var context = scope.ServiceProvider.GetRequiredService<CurrencyDbContext>();
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
    public required string SecurityKey { get; set; }
}