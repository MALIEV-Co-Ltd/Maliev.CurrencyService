using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Maliev.CurrencyService.Data.Interceptors;

public class AuditLogInterceptor : SaveChangesInterceptor
{
    private readonly ILogger<AuditLogInterceptor> _logger;
    private List<string> _tempLogs = new();

    public AuditLogInterceptor(ILogger<AuditLogInterceptor> logger)
    {
        _logger = logger;
    }

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        if (eventData.Context != null)
        {
            _tempLogs = Capture(eventData.Context);
        }
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        if (eventData.Context != null)
        {
            _tempLogs = Capture(eventData.Context);
        }
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        foreach (var log in _tempLogs) _logger.LogInformation("{Log}", log);
        _tempLogs.Clear();
        return base.SavedChanges(eventData, result);
    }

    public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
    {
        foreach (var log in _tempLogs) _logger.LogInformation("{Log}", log);
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
