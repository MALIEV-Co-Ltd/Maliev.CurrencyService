using Maliev.CurrencyService.Data.Models;
using Microsoft.EntityFrameworkCore;
using Maliev.Aspire.ServiceDefaults.Database;

namespace Maliev.CurrencyService.Data;

/// <summary>
/// Database context for the Currency Service
/// </summary>
/// <remarks>
/// Uses snake_case naming convention for all PostgreSQL tables and columns via Npgsql.
/// Includes optimistic concurrency control via Version (row version) columns.
/// Entities: Currency, CountryCurrency, ExchangeRate, RateSnapshot, StagedSnapshot
/// </remarks>
public class CurrencyDbContext : DbContext
{
    private readonly Maliev.CurrencyService.Data.Interceptors.DatabaseMetricsInterceptor? _metricsInterceptor;
    private readonly Maliev.CurrencyService.Data.Interceptors.AuditLogInterceptor? _auditInterceptor;

    /// <summary>
    /// Initializes a new instance of the <see cref="CurrencyDbContext"/> class for testing.
    /// </summary>
    /// <param name="options">The options for this context.</param>
    public CurrencyDbContext(DbContextOptions<CurrencyDbContext> options)
        : base(options)
    {
        _metricsInterceptor = null;
        _auditInterceptor = null;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CurrencyDbContext"/> class.
    /// </summary>
    /// <param name="options">The options for this context.</param>
    /// <param name="metricsInterceptor">The database metrics interceptor.</param>
    /// <param name="auditInterceptor">The audit log interceptor.</param>
    public CurrencyDbContext(
        DbContextOptions<CurrencyDbContext> options,
        Maliev.CurrencyService.Data.Interceptors.DatabaseMetricsInterceptor metricsInterceptor,
        Maliev.CurrencyService.Data.Interceptors.AuditLogInterceptor auditInterceptor)
        : base(options)
    {
        _metricsInterceptor = metricsInterceptor;
        _auditInterceptor = auditInterceptor;
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (_metricsInterceptor != null)
        {
            optionsBuilder.AddInterceptors(_metricsInterceptor);
        }

        if (_auditInterceptor != null)
        {
            optionsBuilder.AddInterceptors(_auditInterceptor);
        }

        base.OnConfiguring(optionsBuilder);
    }

    // Core entities per data-model.md
    /// <summary>
    /// Gets or sets the collection of currencies.
    /// </summary>
    public DbSet<Currency> Currencies => Set<Currency>();
    /// <summary>
    /// Gets or sets the collection of country-currency mappings.
    /// </summary>
    public DbSet<CountryCurrency> CountryCurrencies => Set<CountryCurrency>();
    /// <summary>
    /// Gets or sets the collection of exchange rates.
    /// </summary>
    public DbSet<ExchangeRate> ExchangeRates => Set<ExchangeRate>();
    /// <summary>
    /// Gets or sets the collection of rate snapshots.
    /// </summary>
    public DbSet<RateSnapshot> RateSnapshots => Set<RateSnapshot>();
    /// <summary>
    /// Gets or sets the collection of staged snapshots.
    /// </summary>
    public DbSet<StagedSnapshot> StagedSnapshots => Set<StagedSnapshot>();
    /// <summary>
    /// Gets or sets the collection of audit logs.
    /// </summary>
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    /// <summary>
    /// Gets or sets the collection of batch statuses.
    /// </summary>
    public DbSet<BatchStatus> BatchStatuses => Set<BatchStatus>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Apply all entity configurations from Configurations/ directory
        // This will be used once configurations are created in Task T016
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CurrencyDbContext).Assembly);

        // Apply PostgreSQL snake_case naming convention globally
        SnakeCaseNamingHelper.ApplySnakeCaseNaming(modelBuilder);
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        base.ConfigureConventions(configurationBuilder);

        // Set default string max length to prevent unbounded columns
        configurationBuilder.Properties<string>().HaveMaxLength(256);
    }
}
