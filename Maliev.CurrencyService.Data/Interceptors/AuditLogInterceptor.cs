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
    /// Intercepts the <see cref="DbContext.SaveChanges()"/> method before it is executed.
    /// </summary>
    /// <param name="eventData">The event data.</param>
    /// <param name="result">The result of the operation.</param>
    /// <returns>The <see cref="InterceptionResult{Int32}"/>.</returns>
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        if (eventData.Context is not null)
        {
            AuditChanges(eventData.Context);
        }

        return base.SavingChanges(eventData, result);
    }

    /// <summary>
    /// Intercepts the <see cref="DbContext.SaveChangesAsync(CancellationToken)"/> method before it is executed.
    /// </summary>
    /// <param name="eventData">The event data.</param>
    /// <param name="result">The result of the operation.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A <see cref="ValueTask{TResult}"/> representing the asynchronous operation.</returns>
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is not null)
        {
            AuditChanges(eventData.Context);
        }

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void AuditChanges(DbContext context)
    {
        var timestamp = DateTime.UtcNow;
        var entries = context.ChangeTracker.Entries()
            .Where(e => e.State == EntityState.Added
                     || e.State == EntityState.Modified
                     || e.State == EntityState.Deleted)
            .ToList(); // ToList is crucial to avoid "Collection was modified" exception

        var auditLogs = new List<Maliev.CurrencyService.Data.Models.AuditLog>();

        foreach (var entry in entries)
        {
            if (entry.Entity is Maliev.CurrencyService.Data.Models.AuditLog) continue;

            var entityType = entry.Entity.GetType().Name;
            var operation = entry.State.ToString();

            // Get primary key value
            var keyValues = entry.Properties
                .Where(p => p.Metadata.IsPrimaryKey())
                .Select(p => p.CurrentValue?.ToString() ?? "null")
                .ToList();
            var primaryKey = string.Join(",", keyValues);

            // For updates, capture changed fields
            var changedFields = entry.State == EntityState.Modified
                ? string.Join(", ", entry.Properties
                    .Where(p => p.IsModified)
                    .Select(p => $"{p.Metadata.Name}"))
                : null;

            // Log the audit information
            _logger.LogInformation(
                "Audit: {Operation} on {EntityType} with ID {PrimaryKey} at {Timestamp}. Changed fields: {ChangedFields}",
                operation,
                entityType,
                primaryKey,
                timestamp,
                changedFields ?? "N/A");

            // Create audit log entity
            auditLogs.Add(new Maliev.CurrencyService.Data.Models.AuditLog
            {
                Id = Guid.NewGuid(),
                EntityType = entityType,
                EntityId = primaryKey,
                Operation = operation,
                ChangedFields = changedFields,
                Timestamp = timestamp,
                UserId = "System" // No HTTP context access here
            });
        }

        if (auditLogs.Any())
        {
            context.AddRange(auditLogs);
        }
    }
}
