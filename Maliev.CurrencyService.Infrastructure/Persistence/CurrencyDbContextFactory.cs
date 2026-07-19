using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Maliev.CurrencyService.Infrastructure.Persistence;

/// <summary>
/// Design-time factory for creating <see cref="CurrencyDbContext"/> instances during EF Core migrations.
/// </summary>
public class CurrencyDbContextFactory : IDesignTimeDbContextFactory<CurrencyDbContext>
{
    /// <summary>
    /// Creates a new <see cref="CurrencyDbContext"/> instance for design-time operations.
    /// </summary>
    /// <param name="args">Command-line arguments passed by the EF Core tools.</param>
    /// <returns>A configured <see cref="CurrencyDbContext"/> instance.</returns>
    public CurrencyDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<CurrencyDbContext>();

        // Use a dummy connection string for design-time operations.
        // The actual connection string is provided at runtime via environment/secrets.
        optionsBuilder.UseNpgsql("Host=localhost;Database=currency_design;Username=postgres;Password=postgres");

        return new CurrencyDbContext(optionsBuilder.Options);
    }
}
