using Maliev.CurrencyService.Data.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Maliev.CurrencyService.Data.Configurations;

/// <summary>
/// Entity configuration for Currency
/// </summary>
public class CurrencyConfiguration : IEntityTypeConfiguration<Currency>
{
    /// <summary>
    /// Configures the entity.
    /// </summary>
    /// <param name="builder">The builder to be used to configure the entity type.</param>
    public void Configure(EntityTypeBuilder<Currency> builder)
    {
        // Table and primary key (already configured via attributes, but explicit for clarity)
        builder.ToTable("currencies");
        builder.HasKey(c => c.Id);

        // Alternate key for Code (allows foreign keys to reference Code instead of Id)
        builder.HasAlternateKey(c => c.Code)
            .HasName("ak_currencies_code");

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

        // Version column (kept for migration compatibility, not used for concurrency)
        // Application uses ETag-based optimistic concurrency at API layer
        builder.Property(c => c.Version)
            .HasColumnName("version")
            .HasColumnType("bytea")
            .HasDefaultValue(new byte[8]);

        var defaultDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        builder.HasData(
            new Currency { Id = Guid.Parse("cdb02a5a-6706-52d8-933c-88353a28f2e1"), Code = "AED", Name = "UAE Dirham", Symbol = "AED", DecimalPlaces = 2, IsActive = true, IsPrimary = false, CreatedAt = defaultDate, UpdatedAt = defaultDate, Version = new byte[8] },
            new Currency { Id = Guid.Parse("0b664dfb-74b3-5090-b51d-70b6016f12bb"), Code = "AFN", Name = "Afghani", Symbol = "AFN", DecimalPlaces = 2, IsActive = true, IsPrimary = false, CreatedAt = defaultDate, UpdatedAt = defaultDate, Version = new byte[8] },
            new Currency { Id = Guid.Parse("d2e5096c-f232-5be1-ae33-db2d94754426"), Code = "ALL", Name = "Lek", Symbol = "ALL", DecimalPlaces = 2, IsActive = true, IsPrimary = false, CreatedAt = defaultDate, UpdatedAt = defaultDate, Version = new byte[8] },
            new Currency { Id = Guid.Parse("b30b8bf2-637f-564b-ab62-047b98b221b5"), Code = "AMD", Name = "Armenian Dram", Symbol = "AMD", DecimalPlaces = 2, IsActive = true, IsPrimary = false, CreatedAt = defaultDate, UpdatedAt = defaultDate, Version = new byte[8] },
            new Currency { Id = Guid.Parse("b549d9c7-497e-5134-bd83-ce9082b93254"), Code = "ANG", Name = "Netherlands Antillean Guilder", Symbol = "ANG", DecimalPlaces = 2, IsActive = true, IsPrimary = false, CreatedAt = defaultDate, UpdatedAt = defaultDate, Version = new byte[8] },
            new Currency { Id = Guid.Parse("1b1c3430-bde3-5477-8d98-c5b6d2a8e34b"), Code = "AOA", Name = "Kwanza", Symbol = "AOA", DecimalPlaces = 2, IsActive = true, IsPrimary = false, CreatedAt = defaultDate, UpdatedAt = defaultDate, Version = new byte[8] },
            new Currency { Id = Guid.Parse("0e61aac3-2e9d-587b-af44-f4728802e14f"), Code = "ARS", Name = "Argentine Peso", Symbol = "ARS", DecimalPlaces = 2, IsActive = true, IsPrimary = false, CreatedAt = defaultDate, UpdatedAt = defaultDate, Version = new byte[8] },
            new Currency { Id = Guid.Parse("b0591fe2-6ba4-57bd-a197-3a50c9ec917f"), Code = "AUD", Name = "Australian Dollar", Symbol = "AUD", DecimalPlaces = 2, IsActive = true, IsPrimary = false, CreatedAt = defaultDate, UpdatedAt = defaultDate, Version = new byte[8] },
            new Currency { Id = Guid.Parse("cdc8b8c7-a11d-5b13-a9fa-b61053b896ad"), Code = "AWG", Name = "Aruban Florin", Symbol = "AWG", DecimalPlaces = 2, IsActive = true, IsPrimary = false, CreatedAt = defaultDate, UpdatedAt = defaultDate, Version = new byte[8] },
            new Currency { Id = Guid.Parse("00d04f8c-6061-558d-9920-e4c646fa8317"), Code = "AZN", Name = "Azerbaijan Manat", Symbol = "AZN", DecimalPlaces = 2, IsActive = true, IsPrimary = false, CreatedAt = defaultDate, UpdatedAt = defaultDate, Version = new byte[8] },
            new Currency { Id = Guid.Parse("2f6ed84b-3225-5adc-be10-3e7395313dfa"), Code = "BAM", Name = "Convertible Mark", Symbol = "BAM", DecimalPlaces = 2, IsActive = true, IsPrimary = false, CreatedAt = defaultDate, UpdatedAt = defaultDate, Version = new byte[8] },
            new Currency { Id = Guid.Parse("6ecd80c6-5145-5d3b-9188-5be763b7698f"), Code = "BBD", Name = "Barbados Dollar", Symbol = "BBD", DecimalPlaces = 2, IsActive = true, IsPrimary = false, CreatedAt = defaultDate, UpdatedAt = defaultDate, Version = new byte[8] },
            new Currency { Id = Guid.Parse("82934dd7-414d-5dfd-a678-db928761393a"), Code = "BDT", Name = "Taka", Symbol = "BDT", DecimalPlaces = 2, IsActive = true, IsPrimary = false, CreatedAt = defaultDate, UpdatedAt = defaultDate, Version = new byte[8] },
            new Currency { Id = Guid.Parse("1eef44b7-1242-5c8f-b9a3-04dd1d2bef28"), Code = "BGN", Name = "Bulgarian Lev", Symbol = "BGN", DecimalPlaces = 2, IsActive = true, IsPrimary = false, CreatedAt = defaultDate, UpdatedAt = defaultDate, Version = new byte[8] },
            new Currency { Id = Guid.Parse("b0296673-d608-513f-b0bf-b3cd3ce27a4f"), Code = "BHD", Name = "Bahraini Dinar", Symbol = "BHD", DecimalPlaces = 2, IsActive = true, IsPrimary = false, CreatedAt = defaultDate, UpdatedAt = defaultDate, Version = new byte[8] },
            new Currency { Id = Guid.Parse("3534917e-1501-59b4-9c03-40c697200e89"), Code = "BIF", Name = "Burundi Franc", Symbol = "BIF", DecimalPlaces = 2, IsActive = true, IsPrimary = false, CreatedAt = defaultDate, UpdatedAt = defaultDate, Version = new byte[8] },
            new Currency { Id = Guid.Parse("6475dee3-c4ef-55bc-9e14-515facb414ba"), Code = "BMD", Name = "Bermudian Dollar", Symbol = "BMD", DecimalPlaces = 2, IsActive = true, IsPrimary = false, CreatedAt = defaultDate, UpdatedAt = defaultDate, Version = new byte[8] },
            new Currency { Id = Guid.Parse("74c1473c-5945-5392-831a-449d75b521eb"), Code = "BND", Name = "Brunei Dollar", Symbol = "BND", DecimalPlaces = 2, IsActive = true, IsPrimary = false, CreatedAt = defaultDate, UpdatedAt = defaultDate, Version = new byte[8] },
            new Currency { Id = Guid.Parse("d430e666-b6fc-5fd1-8959-ad9f46c57c93"), Code = "BOB", Name = "Boliviano", Symbol = "BOB", DecimalPlaces = 2, IsActive = true, IsPrimary = false, CreatedAt = defaultDate, UpdatedAt = defaultDate, Version = new byte[8] },
            new Currency { Id = Guid.Parse("6574e823-9f67-519a-983c-9cd89421646f"), Code = "BOV", Name = "Mvdol", Symbol = "BOV", DecimalPlaces = 2, IsActive = true, IsPrimary = false, CreatedAt = defaultDate, UpdatedAt = defaultDate, Version = new byte[8] },
            new Currency { Id = Guid.Parse("c38ec8d2-9fba-5df8-b4c1-17a12da1ef3e"), Code = "BRL", Name = "Brazilian Real", Symbol = "BRL", DecimalPlaces = 2, IsActive = true, IsPrimary = false, CreatedAt = defaultDate, UpdatedAt = defaultDate, Version = new byte[8] },
            new Currency { Id = Guid.Parse("adee4d79-07bb-5e7a-8068-acf35c8a5b05"), Code = "BSD", Name = "Bahamian Dollar", Symbol = "BSD", DecimalPlaces = 2, IsActive = true, IsPrimary = false, CreatedAt = defaultDate, UpdatedAt = defaultDate, Version = new byte[8] },
            new Currency { Id = Guid.Parse("86f5c8ff-98e4-5674-ac03-d2f8fb2e41f0"), Code = "BTN", Name = "Ngultrum", Symbol = "BTN", DecimalPlaces = 2, IsActive = true, IsPrimary = false, CreatedAt = defaultDate, UpdatedAt = defaultDate, Version = new byte[8] },
            new Currency { Id = Guid.Parse("0a5553e7-ff09-5c21-893d-c070eacc4fd7"), Code = "BWP", Name = "Pula", Symbol = "BWP", DecimalPlaces = 2, IsActive = true, IsPrimary = false, CreatedAt = defaultDate, UpdatedAt = defaultDate, Version = new byte[8] },
            new Currency { Id = Guid.Parse("b1bf97bb-897c-5ea9-a1df-1fd70aa6597e"), Code = "BYN", Name = "Belarusian Ruble", Symbol = "BYN", DecimalPlaces = 2, IsActive = true, IsPrimary = false, CreatedAt = defaultDate, UpdatedAt = defaultDate, Version = new byte[8] },
            new Currency { Id = Guid.Parse("28191c96-8412-541e-b3f4-8d4521b560ca"), Code = "BZD", Name = "Belize Dollar", Symbol = "BZD", DecimalPlaces = 2, IsActive = true, IsPrimary = false, CreatedAt = defaultDate, UpdatedAt = defaultDate, Version = new byte[8] },
            new Currency { Id = Guid.Parse("03b24222-bc1d-5201-b72f-68c542513829"), Code = "CAD", Name = "Canadian Dollar", Symbol = "CAD", DecimalPlaces = 2, IsActive = true, IsPrimary = false, CreatedAt = defaultDate, UpdatedAt = defaultDate, Version = new byte[8] },
            new Currency { Id = Guid.Parse("ea7bd63c-f00b-5b10-81cc-496a34c101ed"), Code = "CDF", Name = "Congolese Franc", Symbol = "CDF", DecimalPlaces = 2, IsActive = true, IsPrimary = false, CreatedAt = defaultDate, UpdatedAt = defaultDate, Version = new byte[8] },
            new Currency { Id = Guid.Parse("a79a46f8-e1de-50e2-a925-c66bde9b23f5"), Code = "CHE", Name = "WIR Euro", Symbol = "CHE", DecimalPlaces = 2, IsActive = true, IsPrimary = false, CreatedAt = defaultDate, UpdatedAt = defaultDate, Version = new byte[8] },
            new Currency { Id = Guid.Parse("0ba10dc3-4da7-5b95-b080-37ddcb1f865a"), Code = "CHF", Name = "Swiss Franc", Symbol = "CHF", DecimalPlaces = 2, IsActive = true, IsPrimary = false, CreatedAt = defaultDate, UpdatedAt = defaultDate, Version = new byte[8] },
            new Currency { Id = Guid.Parse("1604e589-7542-5a5e-a31e-70e34b432d18"), Code = "CHW", Name = "WIR Franc", Symbol = "CHW", DecimalPlaces = 2, IsActive = true, IsPrimary = false, CreatedAt = defaultDate, UpdatedAt = defaultDate, Version = new byte[8] },
            new Currency { Id = Guid.Parse("fa931b22-ba5b-5f32-a27f-68387674f01c"), Code = "CLF", Name = "Unidad de Fomento", Symbol = "CLF", DecimalPlaces = 2, IsActive = true, IsPrimary = false, CreatedAt = defaultDate, UpdatedAt = defaultDate, Version = new byte[8] },
            new Currency { Id = Guid.Parse("1abc81ba-455d-579e-8cd2-3741d29c52f4"), Code = "CLP", Name = "Chilean Peso", Symbol = "CLP", DecimalPlaces = 2, IsActive = true, IsPrimary = false, CreatedAt = defaultDate, UpdatedAt = defaultDate, Version = new byte[8] },
            new Currency { Id = Guid.Parse("6dc31208-9eff-58d6-91e6-bc50e3df3433"), Code = "CNY", Name = "Yuan Renminbi", Symbol = "CNY", DecimalPlaces = 2, IsActive = true, IsPrimary = false, CreatedAt = defaultDate, UpdatedAt = defaultDate, Version = new byte[8] },
            new Currency { Id = Guid.Parse("eaf181af-85b8-5f39-8597-b211364b7a35"), Code = "COP", Name = "Colombian Peso", Symbol = "COP", DecimalPlaces = 2, IsActive = true, IsPrimary = false, CreatedAt = defaultDate, UpdatedAt = defaultDate, Version = new byte[8] },
            new Currency { Id = Guid.Parse("177ff8de-07bb-5133-a0c4-0a20fdc9ac36"), Code = "COU", Name = "Unidad de Valor Real", Symbol = "COU", DecimalPlaces = 2, IsActive = true, IsPrimary = false, CreatedAt = defaultDate, UpdatedAt = defaultDate, Version = new byte[8] },
            new Currency { Id = Guid.Parse("abe47642-7b01-5d46-b951-7a0e34d8fbf7"), Code = "CRC", Name = "Costa Rican Colon", Symbol = "CRC", DecimalPlaces = 2, IsActive = true, IsPrimary = false, CreatedAt = defaultDate, UpdatedAt = defaultDate, Version = new byte[8] },
            new Currency { Id = Guid.Parse("9008ce10-78a3-5725-ab99-c6e22285ab05"), Code = "CUC", Name = "Peso Convertible", Symbol = "CUC", DecimalPlaces = 2, IsActive = true, IsPrimary = false, CreatedAt = defaultDate, UpdatedAt = defaultDate, Version = new byte[8] },
            new Currency { Id = Guid.Parse("5958e4d2-4ac3-57e4-9b69-8552173a5840"), Code = "CUP", Name = "Cuban Peso", Symbol = "CUP", DecimalPlaces = 2, IsActive = true, IsPrimary = false, CreatedAt = defaultDate, UpdatedAt = defaultDate, Version = new byte[8] },
            new Currency { Id = Guid.Parse("4abc4f54-12c1-5645-b243-026e3a6b72f8"), Code = "CVE", Name = "Cabo Verde Escudo", Symbol = "CVE", DecimalPlaces = 2, IsActive = true, IsPrimary = false, CreatedAt = defaultDate, UpdatedAt = defaultDate, Version = new byte[8] },
            new Currency { Id = Guid.Parse("ac242c2b-fa0c-5abd-a115-4ec95eb7177a"), Code = "CZK", Name = "Czech Koruna", Symbol = "CZK", DecimalPlaces = 2, IsActive = true, IsPrimary = false, CreatedAt = defaultDate, UpdatedAt = defaultDate, Version = new byte[8] },
            new Currency { Id = Guid.Parse("128728f0-be2e-5d5d-8bee-6cdf16360cae"), Code = "DJF", Name = "Djibouti Franc", Symbol = "DJF", DecimalPlaces = 2, IsActive = true, IsPrimary = false, CreatedAt = defaultDate, UpdatedAt = defaultDate, Version = new byte[8] },
            new Currency { Id = Guid.Parse("d36a6593-2e3d-5a3a-a091-04a9eaa0a9f8"), Code = "DKK", Name = "Danish Krone", Symbol = "DKK", DecimalPlaces = 2, IsActive = true, IsPrimary = false, CreatedAt = defaultDate, UpdatedAt = defaultDate, Version = new byte[8] },
            new Currency { Id = Guid.Parse("5eeeb906-c977-5532-af4a-38577d7585cc"), Code = "DOP", Name = "Dominican Peso", Symbol = "DOP", DecimalPlaces = 2, IsActive = true, IsPrimary = false, CreatedAt = defaultDate, UpdatedAt = defaultDate, Version = new byte[8] },
            new Currency { Id = Guid.Parse("f287591a-ab71-5387-a4ac-229a7bd2d439"), Code = "DZD", Name = "Algerian Dinar", Symbol = "DZD", DecimalPlaces = 2, IsActive = true, IsPrimary = false, CreatedAt = defaultDate, UpdatedAt = defaultDate, Version = new byte[8] },
            new Currency { Id = Guid.Parse("84772f85-aaa3-51ac-a5c3-5ed1b316f70d"), Code = "EGP", Name = "Egyptian Pound", Symbol = "EGP", DecimalPlaces = 2, IsActive = true, IsPrimary = false, CreatedAt = defaultDate, UpdatedAt = defaultDate, Version = new byte[8] },
            new Currency { Id = Guid.Parse("1f4f21c4-d1a0-5af3-bdcc-486f8c6d480e"), Code = "ERN", Name = "Nakfa", Symbol = "ERN", DecimalPlaces = 2, IsActive = true, IsPrimary = false, CreatedAt = defaultDate, UpdatedAt = defaultDate, Version = new byte[8] },
            new Currency { Id = Guid.Parse("c2c78f09-f999-5ab9-9c79-459e5f6211ea"), Code = "ETB", Name = "Ethiopian Birr", Symbol = "ETB", DecimalPlaces = 2, IsActive = true, IsPrimary = false, CreatedAt = defaultDate, UpdatedAt = defaultDate, Version = new byte[8] },
            new Currency { Id = Guid.Parse("80a0c4e7-614b-57a9-b830-9dd2c66196e7"), Code = "EUR", Name = "Euro", Symbol = "EUR", DecimalPlaces = 2, IsActive = true, IsPrimary = false, CreatedAt = defaultDate, UpdatedAt = defaultDate, Version = new byte[8] },
            new Currency { Id = Guid.Parse("faa03b00-8cd9-534a-8d4c-7e0178ed7f43"), Code = "FJD", Name = "Fiji Dollar", Symbol = "FJD", DecimalPlaces = 2, IsActive = true, IsPrimary = false, CreatedAt = defaultDate, UpdatedAt = defaultDate, Version = new byte[8] },
            new Currency { Id = Guid.Parse("d0b8f25d-05a7-51d1-aecc-1299a2cb7fff"), Code = "FKP", Name = "Falkland Islands Pound", Symbol = "FKP", DecimalPlaces = 2, IsActive = true, IsPrimary = false, CreatedAt = defaultDate, UpdatedAt = defaultDate, Version = new byte[8] },
            new Currency { Id = Guid.Parse("035dce62-313b-5d94-a21c-f002f6b7c032"), Code = "GBP", Name = "Pound Sterling", Symbol = "GBP", DecimalPlaces = 2, IsActive = true, IsPrimary = false, CreatedAt = defaultDate, UpdatedAt = defaultDate, Version = new byte[8] },
            new Currency { Id = Guid.Parse("f6ce496f-4a9d-5b58-a23c-5ac925193221"), Code = "GEL", Name = "Lari", Symbol = "GEL", DecimalPlaces = 2, IsActive = true, IsPrimary = false, CreatedAt = defaultDate, UpdatedAt = defaultDate, Version = new byte[8] },
            new Currency { Id = Guid.Parse("6bd7ae66-401a-529f-b0b9-eac83ba366d9"), Code = "GHS", Name = "Ghana Cedi", Symbol = "GHS", DecimalPlaces = 2, IsActive = true, IsPrimary = false, CreatedAt = defaultDate, UpdatedAt = defaultDate, Version = new byte[8] },
            new Currency { Id = Guid.Parse("5b37585b-47a1-56df-a2da-7998805b9add"), Code = "GIP", Name = "Gibraltar Pound", Symbol = "GIP", DecimalPlaces = 2, IsActive = true, IsPrimary = false, CreatedAt = defaultDate, UpdatedAt = defaultDate, Version = new byte[8] },
            new Currency { Id = Guid.Parse("5f35ce79-689c-58da-a410-084a7f6806d0"), Code = "GMD", Name = "Dalasi", Symbol = "GMD", DecimalPlaces = 2, IsActive = true, IsPrimary = false, CreatedAt = defaultDate, UpdatedAt = defaultDate, Version = new byte[8] },
            new Currency { Id = Guid.Parse("4dc02709-e550-598a-bd71-3f1b8ce2e644"), Code = "GNF", Name = "Guinean Franc", Symbol = "GNF", DecimalPlaces = 2, IsActive = true, IsPrimary = false, CreatedAt = defaultDate, UpdatedAt = defaultDate, Version = new byte[8] },
            new Currency { Id = Guid.Parse("7b31c9ea-fcd1-50ba-8277-a6ff72640d3b"), Code = "GTQ", Name = "Quetzal", Symbol = "GTQ", DecimalPlaces = 2, IsActive = true, IsPrimary = false, CreatedAt = defaultDate, UpdatedAt = defaultDate, Version = new byte[8] },
            new Currency { Id = Guid.Parse("43f1113a-546c-55a9-b986-9a53442a461b"), Code = "GYD", Name = "Guyana Dollar", Symbol = "GYD", DecimalPlaces = 2, IsActive = true, IsPrimary = false, CreatedAt = defaultDate, UpdatedAt = defaultDate, Version = new byte[8] },
            new Currency { Id = Guid.Parse("8540368c-dcc1-5d3c-8d78-477424f532bd"), Code = "HKD", Name = "Hong Kong Dollar", Symbol = "HKD", DecimalPlaces = 2, IsActive = true, IsPrimary = false, CreatedAt = defaultDate, UpdatedAt = defaultDate, Version = new byte[8] },
            new Currency { Id = Guid.Parse("91d3948f-c340-5511-aa13-61d3e8829646"), Code = "HNL", Name = "Lempira", Symbol = "HNL", DecimalPlaces = 2, IsActive = true, IsPrimary = false, CreatedAt = defaultDate, UpdatedAt = defaultDate, Version = new byte[8] },
            new Currency { Id = Guid.Parse("3a40472d-13d4-5c3f-8d51-3bb719b3664f"), Code = "HRK", Name = "Kuna", Symbol = "HRK", DecimalPlaces = 2, IsActive = true, IsPrimary = false, CreatedAt = defaultDate, UpdatedAt = defaultDate, Version = new byte[8] },
            new Currency { Id = Guid.Parse("0c11fb91-b454-5a54-95ee-ef75c7540313"), Code = "HTG", Name = "Gourde", Symbol = "HTG", DecimalPlaces = 2, IsActive = true, IsPrimary = false, CreatedAt = defaultDate, UpdatedAt = defaultDate, Version = new byte[8] },
            new Currency { Id = Guid.Parse("d82910b0-610b-5947-af47-000e811df7bc"), Code = "HUF", Name = "Forint", Symbol = "HUF", DecimalPlaces = 2, IsActive = true, IsPrimary = false, CreatedAt = defaultDate, UpdatedAt = defaultDate, Version = new byte[8] },
            new Currency { Id = Guid.Parse("aa592881-be64-5244-8f5b-88ddd7b779ab"), Code = "IDR", Name = "Rupiah", Symbol = "IDR", DecimalPlaces = 2, IsActive = true, IsPrimary = false, CreatedAt = defaultDate, UpdatedAt = defaultDate, Version = new byte[8] },
            new Currency { Id = Guid.Parse("ad324769-4d7d-514a-a710-c20ba386bf2e"), Code = "ILS", Name = "New Israeli Sheqel", Symbol = "ILS", DecimalPlaces = 2, IsActive = true, IsPrimary = false, CreatedAt = defaultDate, UpdatedAt = defaultDate, Version = new byte[8] },
            new Currency { Id = Guid.Parse("2562ab69-202f-5c6a-839e-90f69e9553c5"), Code = "INR", Name = "Indian Rupee", Symbol = "INR", DecimalPlaces = 2, IsActive = true, IsPrimary = false, CreatedAt = defaultDate, UpdatedAt = defaultDate, Version = new byte[8] },
            new Currency { Id = Guid.Parse("6a8ad785-58d5-544c-98be-59decbe8af6b"), Code = "IQD", Name = "Iraqi Dinar", Symbol = "IQD", DecimalPlaces = 2, IsActive = true, IsPrimary = false, CreatedAt = defaultDate, UpdatedAt = defaultDate, Version = new byte[8] },
            new Currency { Id = Guid.Parse("25a2b263-369b-54f9-bc1e-d0030629975b"), Code = "IRR", Name = "Iranian Rial", Symbol = "IRR", DecimalPlaces = 2, IsActive = true, IsPrimary = false, CreatedAt = defaultDate, UpdatedAt = defaultDate, Version = new byte[8] },
            new Currency { Id = Guid.Parse("7cf23d99-7e0d-565d-8a3a-aee2fb424e47"), Code = "ISK", Name = "Iceland Krona", Symbol = "ISK", DecimalPlaces = 2, IsActive = true, IsPrimary = false, CreatedAt = defaultDate, UpdatedAt = defaultDate, Version = new byte[8] },
            new Currency { Id = Guid.Parse("da5194a4-6ed4-5b63-9f67-8bc4701f01ba"), Code = "JMD", Name = "Jamaican Dollar", Symbol = "JMD", DecimalPlaces = 2, IsActive = true, IsPrimary = false, CreatedAt = defaultDate, UpdatedAt = defaultDate, Version = new byte[8] },
            new Currency { Id = Guid.Parse("97a009e8-3777-54a4-98f4-439e957cf1c0"), Code = "JOD", Name = "Jordanian Dinar", Symbol = "JOD", DecimalPlaces = 2, IsActive = true, IsPrimary = false, CreatedAt = defaultDate, UpdatedAt = defaultDate, Version = new byte[8] },
            new Currency { Id = Guid.Parse("ab851d51-efa3-599a-8074-898e8afad236"), Code = "JPY", Name = "Yen", Symbol = "JPY", DecimalPlaces = 2, IsActive = true, IsPrimary = false, CreatedAt = defaultDate, UpdatedAt = defaultDate, Version = new byte[8] },
            new Currency { Id = Guid.Parse("f8e64dcb-3f3b-5f55-b1f6-d10e8f66804b"), Code = "KES", Name = "Kenyan Shilling", Symbol = "KES", DecimalPlaces = 2, IsActive = true, IsPrimary = false, CreatedAt = defaultDate, UpdatedAt = defaultDate, Version = new byte[8] },
            new Currency { Id = Guid.Parse("9122e9e7-c9db-53fd-ad8f-6ff7463c4cc7"), Code = "KGS", Name = "Som", Symbol = "KGS", DecimalPlaces = 2, IsActive = true, IsPrimary = false, CreatedAt = defaultDate, UpdatedAt = defaultDate, Version = new byte[8] },
            new Currency { Id = Guid.Parse("ac69667b-936a-5468-9722-cb53f1b7e050"), Code = "KHR", Name = "Riel", Symbol = "KHR", DecimalPlaces = 2, IsActive = true, IsPrimary = false, CreatedAt = defaultDate, UpdatedAt = defaultDate, Version = new byte[8] },
            new Currency { Id = Guid.Parse("409f569e-1430-5ea2-9931-d6af8eca0042"), Code = "KMF", Name = "Comorian Franc", Symbol = "KMF", DecimalPlaces = 2, IsActive = true, IsPrimary = false, CreatedAt = defaultDate, UpdatedAt = defaultDate, Version = new byte[8] },
            new Currency { Id = Guid.Parse("f92e6e38-1391-5d03-8863-d98e2569df46"), Code = "KPW", Name = "North Korean Won", Symbol = "KPW", DecimalPlaces = 2, IsActive = true, IsPrimary = false, CreatedAt = defaultDate, UpdatedAt = defaultDate, Version = new byte[8] },
            new Currency { Id = Guid.Parse("22b06f67-1c29-57b4-88fa-aec6a4f4d9c2"), Code = "KRW", Name = "Won", Symbol = "KRW", DecimalPlaces = 2, IsActive = true, IsPrimary = false, CreatedAt = defaultDate, UpdatedAt = defaultDate, Version = new byte[8] },
            new Currency { Id = Guid.Parse("4c9c8901-2c6a-50bf-a88f-7c96df7854c9"), Code = "KWD", Name = "Kuwaiti Dinar", Symbol = "KWD", DecimalPlaces = 2, IsActive = true, IsPrimary = false, CreatedAt = defaultDate, UpdatedAt = defaultDate, Version = new byte[8] },
            new Currency { Id = Guid.Parse("ccdf950c-3346-56ca-bfe9-3b3eeedd8109"), Code = "KYD", Name = "Cayman Islands Dollar", Symbol = "KYD", DecimalPlaces = 2, IsActive = true, IsPrimary = false, CreatedAt = defaultDate, UpdatedAt = defaultDate, Version = new byte[8] },
            new Currency { Id = Guid.Parse("5a09356e-e654-5381-8f37-0ce8bfb3291c"), Code = "KZT", Name = "Tenge", Symbol = "KZT", DecimalPlaces = 2, IsActive = true, IsPrimary = false, CreatedAt = defaultDate, UpdatedAt = defaultDate, Version = new byte[8] },
            new Currency { Id = Guid.Parse("8d07b866-1912-5ec5-b9d2-ddf2de594e7d"), Code = "LAK", Name = "Lao Kip", Symbol = "LAK", DecimalPlaces = 2, IsActive = true, IsPrimary = false, CreatedAt = defaultDate, UpdatedAt = defaultDate, Version = new byte[8] },
            new Currency { Id = Guid.Parse("72fbaec0-633d-5db9-aa80-682e07266145"), Code = "LBP", Name = "Lebanese Pound", Symbol = "LBP", DecimalPlaces = 2, IsActive = true, IsPrimary = false, CreatedAt = defaultDate, UpdatedAt = defaultDate, Version = new byte[8] },
            new Currency { Id = Guid.Parse("900ef1f2-6c94-5b5f-a185-95cfcf295cbf"), Code = "LKR", Name = "Sri Lanka Rupee", Symbol = "LKR", DecimalPlaces = 2, IsActive = true, IsPrimary = false, CreatedAt = defaultDate, UpdatedAt = defaultDate, Version = new byte[8] },
            new Currency { Id = Guid.Parse("379cacf6-eab4-5318-9d27-5f0489e2826f"), Code = "LRD", Name = "Liberian Dollar", Symbol = "LRD", DecimalPlaces = 2, IsActive = true, IsPrimary = false, CreatedAt = defaultDate, UpdatedAt = defaultDate, Version = new byte[8] },
            new Currency { Id = Guid.Parse("4c35e46e-d3b4-5522-bcb9-438c95d7313a"), Code = "LSL", Name = "Loti", Symbol = "LSL", DecimalPlaces = 2, IsActive = true, IsPrimary = false, CreatedAt = defaultDate, UpdatedAt = defaultDate, Version = new byte[8] },
            new Currency { Id = Guid.Parse("5ad6bd2a-f3ef-5946-a976-acda759a2e85"), Code = "LYD", Name = "Libyan Dinar", Symbol = "LYD", DecimalPlaces = 2, IsActive = true, IsPrimary = false, CreatedAt = defaultDate, UpdatedAt = defaultDate, Version = new byte[8] },
            new Currency { Id = Guid.Parse("c6bac4d7-f4f8-5877-b44e-1d5fa16cf71e"), Code = "MAD", Name = "Moroccan Dirham", Symbol = "MAD", DecimalPlaces = 2, IsActive = true, IsPrimary = false, CreatedAt = defaultDate, UpdatedAt = defaultDate, Version = new byte[8] },
            new Currency { Id = Guid.Parse("96298897-8c52-52e1-891f-3a180c30524b"), Code = "MDL", Name = "Moldovan Leu", Symbol = "MDL", DecimalPlaces = 2, IsActive = true, IsPrimary = false, CreatedAt = defaultDate, UpdatedAt = defaultDate, Version = new byte[8] },
            new Currency { Id = Guid.Parse("97a2c01d-a01f-5cdf-8688-0435dd4f3f25"), Code = "MGA", Name = "Malagasy Ariary", Symbol = "MGA", DecimalPlaces = 2, IsActive = true, IsPrimary = false, CreatedAt = defaultDate, UpdatedAt = defaultDate, Version = new byte[8] },
            new Currency { Id = Guid.Parse("44b5c0fb-65fe-5416-85a4-c88c4c579b5a"), Code = "MKD", Name = "Denar", Symbol = "MKD", DecimalPlaces = 2, IsActive = true, IsPrimary = false, CreatedAt = defaultDate, UpdatedAt = defaultDate, Version = new byte[8] },
            new Currency { Id = Guid.Parse("8ea7ace8-e579-5c6a-b744-ac84655dbb08"), Code = "MMK", Name = "Kyat", Symbol = "MMK", DecimalPlaces = 2, IsActive = true, IsPrimary = false, CreatedAt = defaultDate, UpdatedAt = defaultDate, Version = new byte[8] },
            new Currency { Id = Guid.Parse("b9c3ef55-277a-5dc7-93dd-f9b70576a78e"), Code = "MNT", Name = "Tugrik", Symbol = "MNT", DecimalPlaces = 2, IsActive = true, IsPrimary = false, CreatedAt = defaultDate, UpdatedAt = defaultDate, Version = new byte[8] },
            new Currency { Id = Guid.Parse("6364813a-3936-5293-b61b-cd954956ee27"), Code = "MOP", Name = "Pataca", Symbol = "MOP", DecimalPlaces = 2, IsActive = true, IsPrimary = false, CreatedAt = defaultDate, UpdatedAt = defaultDate, Version = new byte[8] },
            new Currency { Id = Guid.Parse("2b0280fb-f003-54e8-a394-56b62af79e47"), Code = "MRU", Name = "Ouguiya", Symbol = "MRU", DecimalPlaces = 2, IsActive = true, IsPrimary = false, CreatedAt = defaultDate, UpdatedAt = defaultDate, Version = new byte[8] },
            new Currency { Id = Guid.Parse("5da2aca9-8b5b-5662-afdf-736301228961"), Code = "MUR", Name = "Mauritius Rupee", Symbol = "MUR", DecimalPlaces = 2, IsActive = true, IsPrimary = false, CreatedAt = defaultDate, UpdatedAt = defaultDate, Version = new byte[8] },
            new Currency { Id = Guid.Parse("c8002ff9-758e-5ab1-afb0-46c8d92af05a"), Code = "MVR", Name = "Rufiyaa", Symbol = "MVR", DecimalPlaces = 2, IsActive = true, IsPrimary = false, CreatedAt = defaultDate, UpdatedAt = defaultDate, Version = new byte[8] },
            new Currency { Id = Guid.Parse("d8729e8b-c2a1-5889-b39d-1f0b4c85c4f8"), Code = "MWK", Name = "Malawi Kwacha", Symbol = "MWK", DecimalPlaces = 2, IsActive = true, IsPrimary = false, CreatedAt = defaultDate, UpdatedAt = defaultDate, Version = new byte[8] },
            new Currency { Id = Guid.Parse("03e7fadd-67a8-5e87-94ac-18a727c50dbe"), Code = "MXN", Name = "Mexican Peso", Symbol = "MXN", DecimalPlaces = 2, IsActive = true, IsPrimary = false, CreatedAt = defaultDate, UpdatedAt = defaultDate, Version = new byte[8] },
            new Currency { Id = Guid.Parse("8952a408-7d29-5698-8e2b-f2c4d52ac232"), Code = "MXV", Name = "Mexican Unidad de Inversion (UDI)", Symbol = "MXV", DecimalPlaces = 2, IsActive = true, IsPrimary = false, CreatedAt = defaultDate, UpdatedAt = defaultDate, Version = new byte[8] },
            new Currency { Id = Guid.Parse("107bc6a3-e2c6-5166-8310-50dad1e60193"), Code = "MYR", Name = "Malaysian Ringgit", Symbol = "MYR", DecimalPlaces = 2, IsActive = true, IsPrimary = false, CreatedAt = defaultDate, UpdatedAt = defaultDate, Version = new byte[8] },
            new Currency { Id = Guid.Parse("212a45d1-278a-542b-91bb-fc9f215a4aa4"), Code = "MZN", Name = "Mozambique Metical", Symbol = "MZN", DecimalPlaces = 2, IsActive = true, IsPrimary = false, CreatedAt = defaultDate, UpdatedAt = defaultDate, Version = new byte[8] },
            new Currency { Id = Guid.Parse("80c02c41-b199-5c36-b925-2fc5c2197266"), Code = "NAD", Name = "Namibia Dollar", Symbol = "NAD", DecimalPlaces = 2, IsActive = true, IsPrimary = false, CreatedAt = defaultDate, UpdatedAt = defaultDate, Version = new byte[8] },
            new Currency { Id = Guid.Parse("bed15363-6783-584c-9a37-0f2ee876d7d2"), Code = "NGN", Name = "Naira", Symbol = "NGN", DecimalPlaces = 2, IsActive = true, IsPrimary = false, CreatedAt = defaultDate, UpdatedAt = defaultDate, Version = new byte[8] },
            new Currency { Id = Guid.Parse("2fbc394c-0578-5cd8-a743-4337330ce9cb"), Code = "NIO", Name = "Cordoba Oro", Symbol = "NIO", DecimalPlaces = 2, IsActive = true, IsPrimary = false, CreatedAt = defaultDate, UpdatedAt = defaultDate, Version = new byte[8] },
            new Currency { Id = Guid.Parse("ff49888c-1959-5231-ab4d-4c88611f166f"), Code = "NOK", Name = "Norwegian Krone", Symbol = "NOK", DecimalPlaces = 2, IsActive = true, IsPrimary = false, CreatedAt = defaultDate, UpdatedAt = defaultDate, Version = new byte[8] },
            new Currency { Id = Guid.Parse("9004241c-3454-5d60-b575-d7229cc54b02"), Code = "NPR", Name = "Nepalese Rupee", Symbol = "NPR", DecimalPlaces = 2, IsActive = true, IsPrimary = false, CreatedAt = defaultDate, UpdatedAt = defaultDate, Version = new byte[8] },
            new Currency { Id = Guid.Parse("38fa6ea6-9b61-515c-aa88-97e0fa77d6b1"), Code = "NZD", Name = "New Zealand Dollar", Symbol = "NZD", DecimalPlaces = 2, IsActive = true, IsPrimary = false, CreatedAt = defaultDate, UpdatedAt = defaultDate, Version = new byte[8] },
            new Currency { Id = Guid.Parse("17fbe1df-ab2f-5ee4-a3e6-7aa767650e7d"), Code = "OMR", Name = "Rial Omani", Symbol = "OMR", DecimalPlaces = 2, IsActive = true, IsPrimary = false, CreatedAt = defaultDate, UpdatedAt = defaultDate, Version = new byte[8] },
            new Currency { Id = Guid.Parse("3b4c3126-023a-5760-a95c-f5cba56e36ea"), Code = "PAB", Name = "Balboa", Symbol = "PAB", DecimalPlaces = 2, IsActive = true, IsPrimary = false, CreatedAt = defaultDate, UpdatedAt = defaultDate, Version = new byte[8] },
            new Currency { Id = Guid.Parse("3fe0358e-ba73-52cc-bc0a-b207f16b944a"), Code = "PEN", Name = "Sol", Symbol = "PEN", DecimalPlaces = 2, IsActive = true, IsPrimary = false, CreatedAt = defaultDate, UpdatedAt = defaultDate, Version = new byte[8] },
            new Currency { Id = Guid.Parse("3ded8f55-4e09-5773-a75e-15620a81c80f"), Code = "PGK", Name = "Kina", Symbol = "PGK", DecimalPlaces = 2, IsActive = true, IsPrimary = false, CreatedAt = defaultDate, UpdatedAt = defaultDate, Version = new byte[8] },
            new Currency { Id = Guid.Parse("2dd3cdae-eddb-5944-b851-adc2033a0060"), Code = "PHP", Name = "Philippine Piso", Symbol = "PHP", DecimalPlaces = 2, IsActive = true, IsPrimary = false, CreatedAt = defaultDate, UpdatedAt = defaultDate, Version = new byte[8] },
            new Currency { Id = Guid.Parse("1ad62e26-85a3-531a-9a99-4d4c7f9d1a4b"), Code = "PKR", Name = "Pakistan Rupee", Symbol = "PKR", DecimalPlaces = 2, IsActive = true, IsPrimary = false, CreatedAt = defaultDate, UpdatedAt = defaultDate, Version = new byte[8] },
            new Currency { Id = Guid.Parse("c7132524-7eb9-5b9d-b0ab-6dd544e95690"), Code = "PLN", Name = "Zloty", Symbol = "PLN", DecimalPlaces = 2, IsActive = true, IsPrimary = false, CreatedAt = defaultDate, UpdatedAt = defaultDate, Version = new byte[8] },
            new Currency { Id = Guid.Parse("d61b8fe4-4126-592c-b762-5ac7dad9447a"), Code = "PYG", Name = "Guarani", Symbol = "PYG", DecimalPlaces = 2, IsActive = true, IsPrimary = false, CreatedAt = defaultDate, UpdatedAt = defaultDate, Version = new byte[8] },
            new Currency { Id = Guid.Parse("dd399551-870e-5a39-a1e0-c6367a3fa9ac"), Code = "QAR", Name = "Qatari Rial", Symbol = "QAR", DecimalPlaces = 2, IsActive = true, IsPrimary = false, CreatedAt = defaultDate, UpdatedAt = defaultDate, Version = new byte[8] },
            new Currency { Id = Guid.Parse("d25eb31c-df34-5445-9e79-d988232e4159"), Code = "RON", Name = "Romanian Leu", Symbol = "RON", DecimalPlaces = 2, IsActive = true, IsPrimary = false, CreatedAt = defaultDate, UpdatedAt = defaultDate, Version = new byte[8] },
            new Currency { Id = Guid.Parse("c23e533f-d9b0-514a-9548-276f789fd899"), Code = "RSD", Name = "Serbian Dinar", Symbol = "RSD", DecimalPlaces = 2, IsActive = true, IsPrimary = false, CreatedAt = defaultDate, UpdatedAt = defaultDate, Version = new byte[8] },
            new Currency { Id = Guid.Parse("bfc0b8ac-65ef-5322-87a3-a0bf9dbd547a"), Code = "RUB", Name = "Russian Ruble", Symbol = "RUB", DecimalPlaces = 2, IsActive = true, IsPrimary = false, CreatedAt = defaultDate, UpdatedAt = defaultDate, Version = new byte[8] },
            new Currency { Id = Guid.Parse("36255726-3313-5cf2-9542-78ecff17bbf7"), Code = "RWF", Name = "Rwanda Franc", Symbol = "RWF", DecimalPlaces = 2, IsActive = true, IsPrimary = false, CreatedAt = defaultDate, UpdatedAt = defaultDate, Version = new byte[8] },
            new Currency { Id = Guid.Parse("8cd0267e-bf51-5247-b8cf-e7ab5c06341d"), Code = "SAR", Name = "Saudi Riyal", Symbol = "SAR", DecimalPlaces = 2, IsActive = true, IsPrimary = false, CreatedAt = defaultDate, UpdatedAt = defaultDate, Version = new byte[8] },
            new Currency { Id = Guid.Parse("bb0098b2-c8ab-5f0f-82ca-f7110fc7cab1"), Code = "SBD", Name = "Solomon Islands Dollar", Symbol = "SBD", DecimalPlaces = 2, IsActive = true, IsPrimary = false, CreatedAt = defaultDate, UpdatedAt = defaultDate, Version = new byte[8] },
            new Currency { Id = Guid.Parse("1d97773d-4155-58ea-841f-c6fc274decf5"), Code = "SCR", Name = "Seychelles Rupee", Symbol = "SCR", DecimalPlaces = 2, IsActive = true, IsPrimary = false, CreatedAt = defaultDate, UpdatedAt = defaultDate, Version = new byte[8] },
            new Currency { Id = Guid.Parse("26104f1f-4990-5b82-b258-2fc0276b81ea"), Code = "SDG", Name = "Sudanese Pound", Symbol = "SDG", DecimalPlaces = 2, IsActive = true, IsPrimary = false, CreatedAt = defaultDate, UpdatedAt = defaultDate, Version = new byte[8] },
            new Currency { Id = Guid.Parse("f2a0f264-f8b2-5000-8c70-a27d12a5ba2a"), Code = "SEK", Name = "Swedish Krona", Symbol = "SEK", DecimalPlaces = 2, IsActive = true, IsPrimary = false, CreatedAt = defaultDate, UpdatedAt = defaultDate, Version = new byte[8] },
            new Currency { Id = Guid.Parse("f5129108-0701-5a71-947f-2c3ab4c37e1d"), Code = "SGD", Name = "Singapore Dollar", Symbol = "SGD", DecimalPlaces = 2, IsActive = true, IsPrimary = false, CreatedAt = defaultDate, UpdatedAt = defaultDate, Version = new byte[8] },
            new Currency { Id = Guid.Parse("c250b1a7-10d7-5916-b58c-110466266c26"), Code = "SHP", Name = "Saint Helena Pound", Symbol = "SHP", DecimalPlaces = 2, IsActive = true, IsPrimary = false, CreatedAt = defaultDate, UpdatedAt = defaultDate, Version = new byte[8] },
            new Currency { Id = Guid.Parse("53f81853-8849-5024-9c6a-66738bf597fb"), Code = "SLL", Name = "Leone", Symbol = "SLL", DecimalPlaces = 2, IsActive = true, IsPrimary = false, CreatedAt = defaultDate, UpdatedAt = defaultDate, Version = new byte[8] },
            new Currency { Id = Guid.Parse("5126254a-4e4b-52d6-900d-48c3092b6a14"), Code = "SOS", Name = "Somali Shilling", Symbol = "SOS", DecimalPlaces = 2, IsActive = true, IsPrimary = false, CreatedAt = defaultDate, UpdatedAt = defaultDate, Version = new byte[8] },
            new Currency { Id = Guid.Parse("cc78e296-0db5-585e-a00c-b5da626395b5"), Code = "SRD", Name = "Surinam Dollar", Symbol = "SRD", DecimalPlaces = 2, IsActive = true, IsPrimary = false, CreatedAt = defaultDate, UpdatedAt = defaultDate, Version = new byte[8] },
            new Currency { Id = Guid.Parse("c3e38db3-c18b-54e0-9f62-6b7b07eb2117"), Code = "SSP", Name = "South Sudanese Pound", Symbol = "SSP", DecimalPlaces = 2, IsActive = true, IsPrimary = false, CreatedAt = defaultDate, UpdatedAt = defaultDate, Version = new byte[8] },
            new Currency { Id = Guid.Parse("6d2cc0f3-9b87-598c-8e77-857d99e24da6"), Code = "STN", Name = "Dobra", Symbol = "STN", DecimalPlaces = 2, IsActive = true, IsPrimary = false, CreatedAt = defaultDate, UpdatedAt = defaultDate, Version = new byte[8] },
            new Currency { Id = Guid.Parse("42d99cb3-8272-5202-a69c-4526fd495d1a"), Code = "SVC", Name = "El Salvador Colon", Symbol = "SVC", DecimalPlaces = 2, IsActive = true, IsPrimary = false, CreatedAt = defaultDate, UpdatedAt = defaultDate, Version = new byte[8] },
            new Currency { Id = Guid.Parse("c55815be-34d0-53de-8e03-bd63b06c0f0c"), Code = "SYP", Name = "Syrian Pound", Symbol = "SYP", DecimalPlaces = 2, IsActive = true, IsPrimary = false, CreatedAt = defaultDate, UpdatedAt = defaultDate, Version = new byte[8] },
            new Currency { Id = Guid.Parse("b7e67d9c-2c6c-5e96-a5bb-0d89bb946e9c"), Code = "SZL", Name = "Lilangeni", Symbol = "SZL", DecimalPlaces = 2, IsActive = true, IsPrimary = false, CreatedAt = defaultDate, UpdatedAt = defaultDate, Version = new byte[8] },
            new Currency { Id = Guid.Parse("76a6728a-fe66-5264-a2a2-e1c9e61b3c7d"), Code = "THB", Name = "Baht", Symbol = "THB", DecimalPlaces = 2, IsActive = true, IsPrimary = false, CreatedAt = defaultDate, UpdatedAt = defaultDate, Version = new byte[8] },
            new Currency { Id = Guid.Parse("76e1e4f0-9d79-57ec-9202-28befdf2be56"), Code = "TJS", Name = "Somoni", Symbol = "TJS", DecimalPlaces = 2, IsActive = true, IsPrimary = false, CreatedAt = defaultDate, UpdatedAt = defaultDate, Version = new byte[8] },
            new Currency { Id = Guid.Parse("79e29c47-3abe-5e6e-b8fb-fd07b340daed"), Code = "TMT", Name = "Turkmenistan New Manat", Symbol = "TMT", DecimalPlaces = 2, IsActive = true, IsPrimary = false, CreatedAt = defaultDate, UpdatedAt = defaultDate, Version = new byte[8] },
            new Currency { Id = Guid.Parse("471174a6-e53c-5241-8a88-775944b8b665"), Code = "TND", Name = "Tunisian Dinar", Symbol = "TND", DecimalPlaces = 2, IsActive = true, IsPrimary = false, CreatedAt = defaultDate, UpdatedAt = defaultDate, Version = new byte[8] },
            new Currency { Id = Guid.Parse("5cde17ba-8205-5f50-b8fc-4d79a389d8b8"), Code = "TOP", Name = "Pa’anga", Symbol = "TOP", DecimalPlaces = 2, IsActive = true, IsPrimary = false, CreatedAt = defaultDate, UpdatedAt = defaultDate, Version = new byte[8] },
            new Currency { Id = Guid.Parse("ad0809e7-b5da-521d-b026-8ebee914c200"), Code = "TRY", Name = "Turkish Lira", Symbol = "TRY", DecimalPlaces = 2, IsActive = true, IsPrimary = false, CreatedAt = defaultDate, UpdatedAt = defaultDate, Version = new byte[8] },
            new Currency { Id = Guid.Parse("5205b6fd-8419-5716-8182-e10e024924be"), Code = "TTD", Name = "Trinidad and Tobago Dollar", Symbol = "TTD", DecimalPlaces = 2, IsActive = true, IsPrimary = false, CreatedAt = defaultDate, UpdatedAt = defaultDate, Version = new byte[8] },
            new Currency { Id = Guid.Parse("de60bca8-12f6-5209-b050-f3b8af0b0eb8"), Code = "TWD", Name = "New Taiwan Dollar", Symbol = "TWD", DecimalPlaces = 2, IsActive = true, IsPrimary = false, CreatedAt = defaultDate, UpdatedAt = defaultDate, Version = new byte[8] },
            new Currency { Id = Guid.Parse("6de2e4fa-b977-5003-8e24-c527dd87e03a"), Code = "TZS", Name = "Tanzanian Shilling", Symbol = "TZS", DecimalPlaces = 2, IsActive = true, IsPrimary = false, CreatedAt = defaultDate, UpdatedAt = defaultDate, Version = new byte[8] },
            new Currency { Id = Guid.Parse("5beb6052-7098-5264-af9f-d325b1f79cb2"), Code = "UAH", Name = "Hryvnia", Symbol = "UAH", DecimalPlaces = 2, IsActive = true, IsPrimary = false, CreatedAt = defaultDate, UpdatedAt = defaultDate, Version = new byte[8] },
            new Currency { Id = Guid.Parse("503b09c0-bb41-590c-9c3f-412f3f39dc14"), Code = "UGX", Name = "Uganda Shilling", Symbol = "UGX", DecimalPlaces = 2, IsActive = true, IsPrimary = false, CreatedAt = defaultDate, UpdatedAt = defaultDate, Version = new byte[8] },
            new Currency { Id = Guid.Parse("0eabc243-65ab-5a7e-b7c0-4fcad575af65"), Code = "USD", Name = "US Dollar", Symbol = "USD", DecimalPlaces = 2, IsActive = true, IsPrimary = false, CreatedAt = defaultDate, UpdatedAt = defaultDate, Version = new byte[8] },
            new Currency { Id = Guid.Parse("f7eb384c-9d48-58b7-8ba7-2503d039c6ed"), Code = "USN", Name = "US Dollar (Next day)", Symbol = "USN", DecimalPlaces = 2, IsActive = true, IsPrimary = false, CreatedAt = defaultDate, UpdatedAt = defaultDate, Version = new byte[8] },
            new Currency { Id = Guid.Parse("583e755c-4245-5aa2-a3f6-5f0bb18db385"), Code = "UYI", Name = "Uruguay Peso en Unidades Indexadas (UI)", Symbol = "UYI", DecimalPlaces = 2, IsActive = true, IsPrimary = false, CreatedAt = defaultDate, UpdatedAt = defaultDate, Version = new byte[8] },
            new Currency { Id = Guid.Parse("451dfa94-3c55-5890-a4d4-2ecfa7104214"), Code = "UYU", Name = "Peso Uruguayo", Symbol = "UYU", DecimalPlaces = 2, IsActive = true, IsPrimary = false, CreatedAt = defaultDate, UpdatedAt = defaultDate, Version = new byte[8] },
            new Currency { Id = Guid.Parse("1ce15884-b7f2-5ad3-b23c-9976c280ea91"), Code = "UZS", Name = "Uzbekistan Sum", Symbol = "UZS", DecimalPlaces = 2, IsActive = true, IsPrimary = false, CreatedAt = defaultDate, UpdatedAt = defaultDate, Version = new byte[8] },
            new Currency { Id = Guid.Parse("4cbe6a17-3611-55ed-94c9-725485942beb"), Code = "VEF", Name = "Bolívar", Symbol = "VEF", DecimalPlaces = 2, IsActive = true, IsPrimary = false, CreatedAt = defaultDate, UpdatedAt = defaultDate, Version = new byte[8] },
            new Currency { Id = Guid.Parse("4d021bf7-e8b0-5508-b6e3-848e8e679cc8"), Code = "VND", Name = "Dong", Symbol = "VND", DecimalPlaces = 2, IsActive = true, IsPrimary = false, CreatedAt = defaultDate, UpdatedAt = defaultDate, Version = new byte[8] },
            new Currency { Id = Guid.Parse("c027ada2-236a-5b55-86ea-7546a9964e47"), Code = "VUV", Name = "Vatu", Symbol = "VUV", DecimalPlaces = 2, IsActive = true, IsPrimary = false, CreatedAt = defaultDate, UpdatedAt = defaultDate, Version = new byte[8] },
            new Currency { Id = Guid.Parse("36491270-747a-517b-8ff1-d95575265af7"), Code = "WST", Name = "Tala", Symbol = "WST", DecimalPlaces = 2, IsActive = true, IsPrimary = false, CreatedAt = defaultDate, UpdatedAt = defaultDate, Version = new byte[8] },
            new Currency { Id = Guid.Parse("bb588bda-8638-5567-a860-f600bbbda9c9"), Code = "XAF", Name = "CFA Franc BEAC", Symbol = "XAF", DecimalPlaces = 2, IsActive = true, IsPrimary = false, CreatedAt = defaultDate, UpdatedAt = defaultDate, Version = new byte[8] },
            new Currency { Id = Guid.Parse("8c3abbaa-e465-5b8b-9949-9a67fc3973c0"), Code = "XCD", Name = "East Caribbean Dollar", Symbol = "XCD", DecimalPlaces = 2, IsActive = true, IsPrimary = false, CreatedAt = defaultDate, UpdatedAt = defaultDate, Version = new byte[8] },
            new Currency { Id = Guid.Parse("6dbf8bf1-bae4-5880-838c-0dfeac9700c2"), Code = "XDR", Name = "SDR (Special Drawing Right)", Symbol = "XDR", DecimalPlaces = 2, IsActive = true, IsPrimary = false, CreatedAt = defaultDate, UpdatedAt = defaultDate, Version = new byte[8] },
            new Currency { Id = Guid.Parse("55654a14-505b-5931-a93f-0c68c596ba70"), Code = "XOF", Name = "CFA Franc BCEAO", Symbol = "XOF", DecimalPlaces = 2, IsActive = true, IsPrimary = false, CreatedAt = defaultDate, UpdatedAt = defaultDate, Version = new byte[8] },
            new Currency { Id = Guid.Parse("fc4ea6ab-9eb5-53e3-931d-e4d9b5122682"), Code = "XPF", Name = "CFP Franc", Symbol = "XPF", DecimalPlaces = 2, IsActive = true, IsPrimary = false, CreatedAt = defaultDate, UpdatedAt = defaultDate, Version = new byte[8] },
            new Currency { Id = Guid.Parse("e11107f5-2d80-5ecc-9370-444e11fe1cdd"), Code = "XSU", Name = "Sucre", Symbol = "XSU", DecimalPlaces = 2, IsActive = true, IsPrimary = false, CreatedAt = defaultDate, UpdatedAt = defaultDate, Version = new byte[8] },
            new Currency { Id = Guid.Parse("53d5a9df-a36e-5743-ad90-4b7ccdbf2829"), Code = "XUA", Name = "ADB Unit of Account", Symbol = "XUA", DecimalPlaces = 2, IsActive = true, IsPrimary = false, CreatedAt = defaultDate, UpdatedAt = defaultDate, Version = new byte[8] },
            new Currency { Id = Guid.Parse("a3b7020e-3a9a-547b-92d7-33782014d047"), Code = "YER", Name = "Yemeni Rial", Symbol = "YER", DecimalPlaces = 2, IsActive = true, IsPrimary = false, CreatedAt = defaultDate, UpdatedAt = defaultDate, Version = new byte[8] },
            new Currency { Id = Guid.Parse("048b5050-8ea6-5b21-922a-dcb66d2fd5b5"), Code = "ZAR", Name = "Rand", Symbol = "ZAR", DecimalPlaces = 2, IsActive = true, IsPrimary = false, CreatedAt = defaultDate, UpdatedAt = defaultDate, Version = new byte[8] },
            new Currency { Id = Guid.Parse("a7ca3c2b-c33a-512d-9076-88b4846d095a"), Code = "ZMW", Name = "Zambian Kwacha", Symbol = "ZMW", DecimalPlaces = 2, IsActive = true, IsPrimary = false, CreatedAt = defaultDate, UpdatedAt = defaultDate, Version = new byte[8] },
            new Currency { Id = Guid.Parse("38c68e9c-df78-5b13-a4bf-6695f1165846"), Code = "ZWL", Name = "Zimbabwe Dollar", Symbol = "ZWL", DecimalPlaces = 2, IsActive = true, IsPrimary = false, CreatedAt = defaultDate, UpdatedAt = defaultDate, Version = new byte[8] }
        );

    }
}
