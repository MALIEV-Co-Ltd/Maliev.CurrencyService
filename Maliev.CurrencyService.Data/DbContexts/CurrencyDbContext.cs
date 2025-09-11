using Maliev.CurrencyService.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Maliev.CurrencyService.Data.DbContexts;

public class CurrencyDbContext : DbContext
{
    public CurrencyDbContext(DbContextOptions<CurrencyDbContext> options) : base(options)
    {
    }

    public DbSet<Currency> Currencies { get; set; }
    public DbSet<ExchangeRate> ExchangeRates { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure Currency entity
        modelBuilder.Entity<Currency>(entity =>
        {
            entity.HasKey(e => e.Id);
            
            entity.Property(e => e.ShortName)
                .IsRequired()
                .HasMaxLength(3);

            entity.Property(e => e.LongName)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(e => e.CreatedDate)
                .IsRequired();

            entity.Property(e => e.ModifiedDate)
                .IsRequired();

            // Add unique constraints
            entity.HasIndex(e => e.ShortName)
                .IsUnique()
                .HasDatabaseName("IX_Currencies_ShortName_Unique");

            entity.HasIndex(e => e.LongName)
                .IsUnique()
                .HasDatabaseName("IX_Currencies_LongName_Unique");

            // Add regular index for searching
            entity.HasIndex(e => e.ShortName)
                .HasDatabaseName("IX_Currencies_ShortName");
        });

        // Configure ExchangeRate entity
        modelBuilder.Entity<ExchangeRate>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.FromCurrencyCode)
                .IsRequired()
                .HasMaxLength(3);

            entity.Property(e => e.ToCurrencyCode)
                .IsRequired()
                .HasMaxLength(3);

            entity.Property(e => e.Rate)
                .IsRequired()
                .HasPrecision(18, 8); // High precision for exchange rates

            entity.Property(e => e.FetchedAt)
                .IsRequired();

            entity.Property(e => e.Source)
                .IsRequired()
                .HasMaxLength(50);

            entity.Property(e => e.CreatedDate)
                .IsRequired();

            entity.Property(e => e.ModifiedDate)
                .IsRequired();

            // Add composite index for fast lookups
            entity.HasIndex(e => new { e.FromCurrencyCode, e.ToCurrencyCode })
                .HasDatabaseName("IX_ExchangeRates_FromTo");

            // Add index for recent rates
            entity.HasIndex(e => e.FetchedAt)
                .HasDatabaseName("IX_ExchangeRates_FetchedAt");
        });
    }
}