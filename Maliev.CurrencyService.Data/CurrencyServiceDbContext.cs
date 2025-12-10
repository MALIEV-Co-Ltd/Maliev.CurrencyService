using Maliev.CurrencyService.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace Maliev.CurrencyService.Data;

/// <summary>
/// Database context for the Currency Service
/// </summary>
/// <remarks>
/// Uses snake_case naming convention for all PostgreSQL tables and columns via Npgsql.
/// Includes optimistic concurrency control via Version (row version) columns.
/// Entities: Currency, CountryCurrency, ExchangeRate, RateSnapshot, StagedSnapshot
/// </remarks>
public class CurrencyServiceDbContext : DbContext
{
    private readonly Maliev.CurrencyService.Data.Interceptors.DatabaseMetricsInterceptor _metricsInterceptor;
    private readonly Maliev.CurrencyService.Data.Interceptors.AuditLogInterceptor _auditInterceptor;

    /// <summary>
    /// Initializes a new instance of the <see cref="CurrencyServiceDbContext"/> class.
    /// </summary>
    /// <param name="options">The options for this context.</param>
    /// <param name="metricsInterceptor">The database metrics interceptor.</param>
    /// <param name="auditInterceptor">The audit log interceptor.</param>
    public CurrencyServiceDbContext(
        DbContextOptions<CurrencyServiceDbContext> options,
        Maliev.CurrencyService.Data.Interceptors.DatabaseMetricsInterceptor? metricsInterceptor = null,
        Maliev.CurrencyService.Data.Interceptors.AuditLogInterceptor? auditInterceptor = null)
        : base(options)
    {
        _metricsInterceptor = metricsInterceptor!;
        _auditInterceptor = auditInterceptor!;
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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Apply all entity configurations from Configurations/ directory
        // This will be used once configurations are created in Task T016
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CurrencyServiceDbContext).Assembly);
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        base.ConfigureConventions(configurationBuilder);

        // Set default string max length to prevent unbounded columns
        configurationBuilder.Properties<string>().HaveMaxLength(256);
    }
}
