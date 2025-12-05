using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace Maliev.CurrencyService.Data;

/// <summary>
/// Design-time factory for CurrencyServiceDbContext
/// </summary>
/// <remarks>
/// Used by EF Core tools (dotnet ef migrations add, dotnet ef database update)
/// to create DbContext instances at design time.
/// Reads connection string from environment variable or uses default localhost connection.
/// </remarks>
public class CurrencyServiceDbContextFactory : IDesignTimeDbContextFactory<CurrencyServiceDbContext>
{
    /// <summary>
    /// Creates a new instance of a design-time context.
    /// </summary>
    /// <param name="args">Command line arguments.</param>
    /// <returns>A new <see cref="CurrencyServiceDbContext"/> instance.</returns>
    public CurrencyServiceDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<CurrencyServiceDbContext>();

        // Try to get connection string from environment variable first
        // This allows flexibility during migrations: dotnet ef migrations add --connection "..."
        var connectionString = Environment.GetEnvironmentVariable("ServiceDbContext")
            ?? Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
            ?? "Server=localhost;Port=5432;Database=currency_app_db;User Id=postgres;Password=postgres123;";

        optionsBuilder.UseNpgsql(connectionString, npgsqlOptions =>
        {
            npgsqlOptions.MigrationsAssembly(typeof(CurrencyServiceDbContext).Assembly.GetName().Name);
            npgsqlOptions.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery);
        });

        // Enable sensitive data logging for development only
        if (Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development")
        {
            optionsBuilder.EnableSensitiveDataLogging();
            optionsBuilder.EnableDetailedErrors();
        }

        return new CurrencyServiceDbContext(optionsBuilder.Options);
    }
}
