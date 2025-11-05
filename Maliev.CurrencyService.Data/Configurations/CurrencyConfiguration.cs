using Maliev.CurrencyService.Data.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Maliev.CurrencyService.Data.Configurations;

/// <summary>
/// Entity configuration for Currency
/// </summary>
public class CurrencyConfiguration : IEntityTypeConfiguration<Currency>
{
    public void Configure(EntityTypeBuilder<Currency> builder)
    {
        // Table and primary key (already configured via attributes, but explicit for clarity)
        builder.ToTable("currencies");
        builder.HasKey(c => c.Id);

        // Indexes
        builder.HasIndex(c => c.Code)
            .IsUnique()
            .HasDatabaseName("ix_currencies_code");

        builder.HasIndex(c => c.IsActive)
            .HasDatabaseName("ix_currencies_is_active");

        // Column configurations (complement attributes)
        builder.Property(c => c.Code)
            .IsRequired()
            .HasMaxLength(3)
            .IsUnicode(false); // ASCII only for currency codes

        builder.Property(c => c.Symbol)
            .IsRequired()
            .HasMaxLength(10);

        builder.Property(c => c.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(c => c.DecimalPlaces)
            .IsRequired()
            .HasDefaultValue(2);

        builder.Property(c => c.IsActive)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(c => c.IsPrimary)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(c => c.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("NOW()");

        builder.Property(c => c.UpdatedAt)
            .IsRequired()
            .HasDefaultValueSql("NOW()");

        // Optimistic concurrency via Version (row version)
        builder.Property(c => c.Version)
            .IsRowVersion()
            .HasDefaultValue(new byte[8]);
    }
}
