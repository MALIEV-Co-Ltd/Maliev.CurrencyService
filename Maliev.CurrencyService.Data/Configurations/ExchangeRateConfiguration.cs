using Maliev.CurrencyService.Data.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Maliev.CurrencyService.Data.Configurations;

/// <summary>
/// Entity configuration for ExchangeRate
/// </summary>
public class ExchangeRateConfiguration : IEntityTypeConfiguration<ExchangeRate>
{
    /// <summary>
    /// Configures the entity.
    /// </summary>
    /// <param name="builder">The builder to be used to configure the entity type.</param>
    public void Configure(EntityTypeBuilder<ExchangeRate> builder)
    {
        // Table and primary key
        builder.ToTable("exchange_rates");
        builder.HasKey(er => er.Id);

        // Unique index to prevent duplicate rates for same pair/time
        builder.HasIndex(er => new { er.FromCurrency, er.ToCurrency, er.FetchedAt })
            .IsUnique()
            .HasDatabaseName("ix_exchange_rates_from_to_fetched");

        // Index for rate lookup
        builder.HasIndex(er => new { er.FromCurrency, er.ToCurrency })
            .HasDatabaseName("ix_exchange_rates_from_to");

        // Index for cleanup of expired entries
        builder.HasIndex(er => er.ExpiresAt)
            .HasDatabaseName("ix_exchange_rates_expires_at");

        // Column configurations
        builder.Property(er => er.FromCurrency)
            .IsRequired()
            .HasMaxLength(3)
            .IsUnicode(false); // ASCII only for currency codes

        builder.Property(er => er.ToCurrency)
            .IsRequired()
            .HasMaxLength(3)
            .IsUnicode(false); // ASCII only for currency codes

        builder.Property(er => er.Rate)
            .IsRequired()
            .HasPrecision(18, 6); // 6 decimal precision per SC-012

        builder.Property(er => er.Provider)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(er => er.IsTransitive)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(er => er.IntermediateCurrency)
            .HasMaxLength(3)
            .IsUnicode(false); // ASCII only for currency codes

        builder.Property(er => er.FetchedAt)
            .IsRequired();

        builder.Property(er => er.ExpiresAt)
            .IsRequired();

        builder.Property(er => er.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("NOW()");

        builder.Property(er => er.UpdatedAt)
            .IsRequired()
            .HasDefaultValueSql("NOW()");
    }
}
