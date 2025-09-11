using Maliev.CurrencyService.Data.DbContexts;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;

namespace Maliev.CurrencyService.Api.HealthChecks;

public class DatabaseHealthCheck : IHealthCheck
{
    private readonly CurrencyDbContext _context;

    public DatabaseHealthCheck(CurrencyDbContext context)
    {
        _context = context;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            // Simple database connectivity check
            await _context.Database.CanConnectAsync(cancellationToken);
            
            // Check if we can query the currencies table
            var currencyCount = await _context.Currencies.CountAsync(cancellationToken);
            
            return HealthCheckResult.Healthy($"Database is healthy with {currencyCount} currencies");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy($"Database health check failed: {ex.Message}", ex);
        }
    }
}