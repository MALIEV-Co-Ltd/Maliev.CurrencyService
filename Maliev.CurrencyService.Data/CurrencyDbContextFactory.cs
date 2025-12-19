using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Maliev.CurrencyService.Data;

/// <summary>
/// Design-time factory for creating CurrencyDbContext instances during migrations.
/// </summary>
public class CurrencyDbContextFactory : IDesignTimeDbContextFactory<CurrencyDbContext>
{
    public CurrencyDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<CurrencyDbContext>();

        // Use a dummy connection string for design-time operations
        // The actual connection string is provided at runtime
        optionsBuilder.UseNpgsql("Host=localhost;Database=currency_design;Username=postgres;Password=postgres");

        return new CurrencyDbContext(optionsBuilder.Options);
    }
}
