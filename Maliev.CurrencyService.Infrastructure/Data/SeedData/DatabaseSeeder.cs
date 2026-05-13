using Maliev.CurrencyService.Domain.Entities;
using Maliev.CurrencyService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Maliev.CurrencyService.Infrastructure.Data.SeedData;

/// <summary>
/// Handles initial database seeding for currency data.
/// </summary>
public static class DatabaseSeeder
{
    /// <summary>
    /// Seeds the currencies table from the bundled C# seed data if the table is empty.
    /// Uses a single multi-row INSERT to minimize database round-trips.
    /// </summary>
    /// <param name="host">The application host to resolve services from.</param>
    /// <returns>A task representing the asynchronous seeding operation.</returns>
    public static async Task SeedCurrenciesAsync(this IHost host)
    {
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<CurrencyDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("DatabaseSeeder");

        try
        {
            var hasCurrencies = await context.Currencies.AnyAsync();
            if (hasCurrencies)
            {
                logger.LogInformation("Currencies table already has data. Skipping seed.");
            }
            else
            {
                logger.LogInformation("Seeding currencies from C# seed data...");
                var currencies = CurrencySeedData.GetAll().ToList();

                var strategy = context.Database.CreateExecutionStrategy();
                await strategy.ExecuteAsync(async () =>
                {
                    await BulkInsertCurrenciesAsync(context, currencies);
                    logger.LogInformation("Successfully seeded {Count} currencies.", currencies.Count);
                });
            }

            await SeedCountryCurrencyMappingsAsync(context, logger);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred while seeding the database.");
        }
    }

    private static async Task BulkInsertCurrenciesAsync(CurrencyDbContext context, IReadOnlyList<Currency> currencies)
    {
        if (currencies.Count == 0) return;

        var now = DateTime.UtcNow;
        foreach (var currency in currencies)
        {
            currency.CreatedAt = currency.CreatedAt != default ? currency.CreatedAt : now;
            currency.UpdatedAt = currency.UpdatedAt != default ? currency.UpdatedAt : now;
        }

        await context.Currencies.AddRangeAsync(currencies);
        await context.SaveChangesAsync();
    }

    private static async Task SeedCountryCurrencyMappingsAsync(CurrencyDbContext context, ILogger logger)
    {
        var mappings = new[]
        {
            new CountryCurrency
            {
                Id = Guid.NewGuid(),
                CountryIso2 = "TH",
                CountryIso3 = "THA",
                CurrencyCode = "THB",
                IsPrimary = true,
                CreatedAt = DateTime.UtcNow
            },
            new CountryCurrency
            {
                Id = Guid.NewGuid(),
                CountryIso2 = "US",
                CountryIso3 = "USA",
                CurrencyCode = "USD",
                IsPrimary = true,
                CreatedAt = DateTime.UtcNow
            }
        };

        var added = 0;
        foreach (var mapping in mappings)
        {
            var exists = await context.CountryCurrencies.AnyAsync(
                c => c.CountryIso2 == mapping.CountryIso2 && c.CurrencyCode == mapping.CurrencyCode);
            if (exists)
            {
                continue;
            }

            context.CountryCurrencies.Add(mapping);
            added++;
        }

        if (added > 0)
        {
            await context.SaveChangesAsync();
            logger.LogInformation("Seeded {Count} country-currency mappings.", added);
        }
    }
}
