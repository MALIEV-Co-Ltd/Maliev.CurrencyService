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
            if (await context.Currencies.AnyAsync())
            {
                logger.LogInformation("Currencies table already has data. Skipping seed.");
                return;
            }

            logger.LogInformation("Seeding currencies from C# seed data...");
            var currencies = CurrencySeedData.GetAll().ToList();

            var strategy = context.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                await context.Currencies.AddRangeAsync(currencies);
                await context.SaveChangesAsync();
                logger.LogInformation("Successfully seeded {Count} currencies.", currencies.Count);
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred while seeding the database.");
        }
    }
}
