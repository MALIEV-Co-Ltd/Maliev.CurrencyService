using Maliev.Aspire.ServiceDefaults.Database;
using Maliev.CurrencyService.Domain.Entities;
using Maliev.CurrencyService.Infrastructure.Persistence.Interceptors;
using Microsoft.EntityFrameworkCore;

namespace Maliev.CurrencyService.Infrastructure.Persistence;

/// <summary>
/// Database context for the Currency Service.
/// </summary>
/// <remarks>
/// Uses snake_case naming convention for all PostgreSQL tables and columns via Npgsql.
/// Includes optimistic concurrency control via Version (row version) columns.
/// Entities: Currency, CountryCurrency, ExchangeRate, RateSnapshot, StagedSnapshot.
/// </remarks>
public class CurrencyDbContext : DbContext
{
    private readonly DatabaseMetricsInterceptor? _metricsInterceptor;
    private readonly AuditLogInterceptor? _auditInterceptor;

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
    /// Initializes a new instance of the <see cref="CurrencyDbContext"/> class with interceptors.
    /// </summary>
    /// <param name="options">The options for this context.</param>
    /// <param name="metricsInterceptor">The database metrics interceptor.</param>
    /// <param name="auditInterceptor">The audit log interceptor.</param>
    public CurrencyDbContext(
        DbContextOptions<CurrencyDbContext> options,
        DatabaseMetricsInterceptor metricsInterceptor,
        AuditLogInterceptor auditInterceptor)
        : base(options)
    {
        _metricsInterceptor = metricsInterceptor;
        _auditInterceptor = auditInterceptor;
    }

    /// <inheritdoc />
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

    /// <summary>Gets the collection of currencies.</summary>
    public DbSet<Currency> Currencies => Set<Currency>();

    /// <summary>Gets the collection of country-currency mappings.</summary>
    public DbSet<CountryCurrency> CountryCurrencies => Set<CountryCurrency>();

    /// <summary>Gets the collection of cached live exchange rates.</summary>
    public DbSet<ExchangeRate> ExchangeRates => Set<ExchangeRate>();

    /// <summary>Gets the collection of historical rate snapshots.</summary>
    public DbSet<RateSnapshot> RateSnapshots => Set<RateSnapshot>();

    /// <summary>Gets the collection of staged snapshots awaiting promotion.</summary>
    public DbSet<StagedSnapshot> StagedSnapshots => Set<StagedSnapshot>();

    /// <summary>Gets the collection of audit log entries.</summary>
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    /// <summary>Gets the collection of batch status records.</summary>
    public DbSet<BatchStatus> BatchStatuses => Set<BatchStatus>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Apply all entity configurations from Configurations/ directory
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CurrencyDbContext).Assembly);

        // Apply PostgreSQL snake_case naming convention globally
        SnakeCaseNamingHelper.ApplySnakeCaseNaming(modelBuilder);
    }

    /// <inheritdoc />
    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        base.ConfigureConventions(configurationBuilder);

        // Set default string max length to prevent unbounded columns
        configurationBuilder.Properties<string>().HaveMaxLength(256);
    }
}
