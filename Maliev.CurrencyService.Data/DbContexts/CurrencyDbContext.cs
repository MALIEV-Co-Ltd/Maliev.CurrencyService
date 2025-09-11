using Maliev.CurrencyService.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Maliev.CurrencyService.Data.DbContexts;

public class CurrencyDbContext : DbContext
{
    public CurrencyDbContext(DbContextOptions<CurrencyDbContext> options) : base(options)
    {
    }

    public DbSet<Currency> Currencies { get; set; }

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
    }
}