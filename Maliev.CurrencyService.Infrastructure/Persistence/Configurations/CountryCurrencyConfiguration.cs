using Maliev.CurrencyService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Maliev.CurrencyService.Infrastructure.Persistence.Configurations;

/// <summary>
/// Entity configuration for CountryCurrency.
/// </summary>
public class CountryCurrencyConfiguration : IEntityTypeConfiguration<CountryCurrency>
{
    /// <summary>
    /// Configures the entity.
    /// </summary>
    /// <param name="builder">The builder to be used to configure the entity type.</param>
    public void Configure(EntityTypeBuilder<CountryCurrency> builder)
    {
        builder.ToTable("country_currencies");
        builder.HasKey(cc => cc.Id);

        builder.HasIndex(cc => new { cc.CountryIso2, cc.CurrencyCode })
            .IsUnique()
            .HasDatabaseName("ix_country_iso2_currency");

        builder.HasIndex(cc => new { cc.CountryIso3, cc.CurrencyCode })
            .IsUnique()
            .HasDatabaseName("ix_country_iso3_currency");

        builder.HasIndex(cc => cc.CurrencyCode)
            .HasDatabaseName("ix_country_currencies_currency_code");

        builder.Property(cc => cc.CountryIso2)
            .IsRequired()
            .HasMaxLength(2)
            .IsUnicode(false);

        builder.Property(cc => cc.CountryIso3)
            .IsRequired()
            .HasMaxLength(3)
            .IsUnicode(false);

        builder.Property(cc => cc.CurrencyCode)
            .IsRequired()
            .HasMaxLength(3)
            .IsUnicode(false);

        builder.Property(cc => cc.IsPrimary)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(cc => cc.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("NOW()");

        builder.HasOne(cc => cc.Currency)
            .WithMany()
            .HasForeignKey(cc => cc.CurrencyCode)
            .HasPrincipalKey(c => c.Code)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
