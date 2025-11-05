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
    public CurrencyServiceDbContext(DbContextOptions<CurrencyServiceDbContext> options)
        : base(options)
    {
    }

    // Core entities per data-model.md
    public DbSet<Currency> Currencies => Set<Currency>();
    public DbSet<CountryCurrency> CountryCurrencies => Set<CountryCurrency>();
    public DbSet<ExchangeRate> ExchangeRates => Set<ExchangeRate>();
    public DbSet<RateSnapshot> RateSnapshots => Set<RateSnapshot>();
    public DbSet<StagedSnapshot> StagedSnapshots => Set<StagedSnapshot>();

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
