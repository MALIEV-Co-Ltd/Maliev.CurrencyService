using Maliev.CurrencyService.Data.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Maliev.CurrencyService.Data.Configurations;

/// <summary>
/// Entity configuration for CountryCurrency
/// </summary>
public class CountryCurrencyConfiguration : IEntityTypeConfiguration<CountryCurrency>
{
    /// <summary>
    /// Configures the entity of type <typeparamref name="TEntity" />.
    /// </summary>
    /// <param name="builder">The builder to be used to configure the entity type.</param>
    public void Configure(EntityTypeBuilder<CountryCurrency> builder)
    {
        // Table and primary key
        builder.ToTable("country_currencies");
        builder.HasKey(cc => cc.Id);

        // Unique composite indexes (already configured via attributes, but explicit)
        builder.HasIndex(cc => new { cc.CountryIso2, cc.CurrencyCode })
            .IsUnique()
            .HasDatabaseName("ix_country_iso2_currency");

        builder.HasIndex(cc => new { cc.CountryIso3, cc.CurrencyCode })
            .IsUnique()
            .HasDatabaseName("ix_country_iso3_currency");

        // Foreign key index for performance
        builder.HasIndex(cc => cc.CurrencyCode)
            .HasDatabaseName("ix_country_currencies_currency_code");

        // Column configurations
        builder.Property(cc => cc.CountryIso2)
            .IsRequired()
            .HasMaxLength(2)
            .IsUnicode(false); // ASCII only for ISO codes

        builder.Property(cc => cc.CountryIso3)
            .IsRequired()
            .HasMaxLength(3)
            .IsUnicode(false); // ASCII only for ISO codes

        builder.Property(cc => cc.CurrencyCode)
            .IsRequired()
            .HasMaxLength(3)
            .IsUnicode(false); // ASCII only for currency codes

        builder.Property(cc => cc.IsPrimary)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(cc => cc.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("NOW()");

        // Foreign key relationship to Currency
        // RESTRICT on delete - cannot delete currency if countries use it
        builder.HasOne(cc => cc.Currency)
            .WithMany()
            .HasForeignKey(cc => cc.CurrencyCode)
            .HasPrincipalKey(c => c.Code)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
