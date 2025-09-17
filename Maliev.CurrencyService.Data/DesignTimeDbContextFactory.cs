using Maliev.CurrencyService.Data.DbContexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Maliev.CurrencyService.Data
{
    public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<CurrencyDbContext>
    {
        public CurrencyDbContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<CurrencyDbContext>();

            var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__CurrencyDbContext");

            if (string.IsNullOrEmpty(connectionString))
            {
                connectionString = "Host=localhost;Port=5433;Database=currency_app_db;Username=postgres;Password=temp;SslMode=Disable";
            }

            optionsBuilder.UseNpgsql(connectionString);

            return new CurrencyDbContext(optionsBuilder.Options);
        }
    }
}