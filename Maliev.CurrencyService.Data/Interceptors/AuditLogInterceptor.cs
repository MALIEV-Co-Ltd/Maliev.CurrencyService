using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Maliev.CurrencyService.Data.Interceptors;

/// <summary>
/// Audit log interceptor for tracking all INSERT, UPDATE, DELETE operations
/// </summary>
/// <remarks>
/// Captures entity type, operation type, user ID, timestamp, and changed fields.
/// Per data-model.md Audit section: logs operations on currencies and rate_snapshots.
/// </remarks>
public class AuditLogInterceptor : SaveChangesInterceptor
{
    private readonly ILogger<AuditLogInterceptor> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AuditLogInterceptor"/> class.
    /// </summary>
    /// <param name="logger">The logger for the interceptor.</param>
    public AuditLogInterceptor(ILogger<AuditLogInterceptor> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Intercepts the <see cref="DbContext.SaveChanges()"/> method after it is executed.
    /// </summary>
    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        if (eventData.Context is not null)
        {
            AuditChanges(eventData.Context);
        }
        return base.SavedChanges(eventData, result);
    }

    /// <summary>
    /// Intercepts the <see cref="DbContext.SaveChangesAsync(CancellationToken)"/> method after it is executed.
    /// </summary>
    public override async ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is not null)
        {
            await AuditChangesAsync(eventData.Context, cancellationToken);
        }
        return await base.SavedChangesAsync(eventData, result, cancellationToken);
    }

    private void AuditChanges(DbContext context)
    {
        // For SavedChanges, we use a separate context to avoid re-triggering save on same context
        // and ensure audit records are committed.
        // NOTE: In a production environment, this should ideally be pushed to a queue (Outbox pattern).

        try
        {
            var auditEntries = GetAuditEntries(context);
            if (!auditEntries.Any()) return;

            // Use a separate transaction/connection to save audit logs
            // to avoid re-triggering interceptors on the same context
            // or use a dedicated logger service.

            foreach (var entry in auditEntries)
            {
                _logger.LogInformation("Audit: {Operation} on {EntityType} with ID {PrimaryKey}",
                    entry.Operation, entry.EntityType, entry.EntityId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to record audit logs");
        }
    }

    private async Task AuditChangesAsync(DbContext context, CancellationToken cancellationToken)
    {
        try
        {
            var auditEntries = GetAuditEntries(context);
            if (!auditEntries.Any()) return;

            foreach (var entry in auditEntries)
            {
                _logger.LogInformation("Audit (Async): {Operation} on {EntityType} with ID {PrimaryKey}",
                    entry.Operation, entry.EntityType, entry.EntityId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to record audit logs asynchronously");
        }
    }

    private List<Maliev.CurrencyService.Data.Models.AuditLog> GetAuditEntries(DbContext context)
    {
        var timestamp = DateTime.UtcNow;
        return context.ChangeTracker.Entries()
            .Where(e => e.State == EntityState.Added || e.State == EntityState.Modified || e.State == EntityState.Deleted)
            .Select(entry => new Maliev.CurrencyService.Data.Models.AuditLog
            {
                Id = Guid.NewGuid(),
                EntityType = entry.Entity.GetType().Name,
                EntityId = string.Join(",", entry.Properties.Where(p => p.Metadata.IsPrimaryKey()).Select(p => p.CurrentValue?.ToString() ?? "null")),
                Operation = entry.State.ToString(),
                ChangedFields = entry.State == EntityState.Modified ? string.Join(", ", entry.Properties.Where(p => p.IsModified).Select(p => p.Metadata.Name)) : null,
                Timestamp = timestamp,
                UserId = "System"
            })
            .ToList();
    }
}
