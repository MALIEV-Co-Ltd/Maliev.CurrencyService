using Maliev.CurrencyService.Data;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;

namespace Maliev.CurrencyService.Api.HealthChecks;

/// <summary>
/// Health check for the database connection and basic query functionality.
/// </summary>
public class DatabaseHealthCheck : IHealthCheck
{
    private readonly CurrencyServiceDbContext _context;

    /// <summary>
    /// Initializes a new instance of the <see cref="DatabaseHealthCheck"/> class.
    /// </summary>
    /// <param name="context">The database context.</param>
    public DatabaseHealthCheck(CurrencyServiceDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Checks the health of the database connection and performs a simple query.
    /// </summary>
    /// <param name="context">A context object associated with the current health check.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/> that can be used to cancel the health check.</param>
    /// <returns>A <see cref="Task"/> that completes when the health check has finished, yielding a <see cref="HealthCheckResult"/>.</returns>
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