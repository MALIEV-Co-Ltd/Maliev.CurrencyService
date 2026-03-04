using Maliev.CurrencyService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Maliev.CurrencyService.Infrastructure.Persistence.Configurations;

/// <summary>
/// Entity configuration for Currency.
/// </summary>
public class CurrencyConfiguration : IEntityTypeConfiguration<Currency>
{
    /// <summary>
    /// Configures the entity.
    /// </summary>
    /// <param name="builder">The builder to be used to configure the entity type.</param>
    public void Configure(EntityTypeBuilder<Currency> builder)
    {
        builder.ToTable("currencies");
        builder.HasKey(c => c.Id);

        builder.HasAlternateKey(c => c.Code)
            .HasName("ak_currencies_code");

        builder.HasIndex(c => c.Code)
            .IsUnique()
            .HasDatabaseName("ix_currencies_code");

        builder.HasIndex(c => c.IsActive)
            .HasDatabaseName("ix_currencies_is_active");

        builder.Property(c => c.Code)
            .IsRequired()
            .HasMaxLength(3)
            .IsUnicode(false);

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
    }
}
