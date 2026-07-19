using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Maliev.CurrencyService.Infrastructure.Persistence.Interceptors;

/// <summary>
/// Captures entity state changes before and after save operations for audit logging.
/// </summary>
public class AuditLogInterceptor : SaveChangesInterceptor
{
    private readonly ILogger<AuditLogInterceptor> _logger;
    private List<string> _tempLogs = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="AuditLogInterceptor"/> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    public AuditLogInterceptor(ILogger<AuditLogInterceptor> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        if (eventData.Context != null)
        {
            _tempLogs = Capture(eventData.Context);
        }
        return base.SavingChanges(eventData, result);
    }

    /// <inheritdoc />
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        if (eventData.Context != null)
        {
            _tempLogs = Capture(eventData.Context);
        }
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    /// <inheritdoc />
    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        foreach (var log in _tempLogs) _logger.LogDebug("{Log}", log);
        _tempLogs.Clear();
        return base.SavedChanges(eventData, result);
    }

    /// <inheritdoc />
    public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
    {
        foreach (var log in _tempLogs) _logger.LogDebug("{Log}", log);
        _tempLogs.Clear();
        return base.SavedChangesAsync(eventData, result, cancellationToken);
    }

    private List<string> Capture(DbContext context)
    {
        return context.ChangeTracker.Entries()
            .Where(e => e.State == EntityState.Added || e.State == EntityState.Modified || e.State == EntityState.Deleted)
            .Select(e => $"Audit: {e.State} on {e.Entity.GetType().Name}")
            .ToList();
    }
}
