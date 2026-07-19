namespace Maliev.CurrencyService.Domain.Interfaces;

/// <summary>
/// Interface for recording database operation metrics.
/// </summary>
public interface IDatabaseMetrics
{
    /// <summary>
    /// Records a database query execution event.
    /// </summary>
    /// <param name="operation">The SQL operation type (SELECT, INSERT, UPDATE, DELETE).</param>
    void RecordDatabaseQuery(string operation);

    /// <summary>
    /// Records the duration of a database query.
    /// </summary>
    /// <param name="operation">The SQL operation type.</param>
    /// <param name="durationSeconds">The query duration in seconds.</param>
    void RecordDatabaseQueryDuration(string operation, double durationSeconds);

    /// <summary>
    /// Records a database error event.
    /// </summary>
    /// <param name="operation">The SQL operation type that failed.</param>
    /// <param name="errorType">The type of error that occurred.</param>
    void RecordDatabaseError(string operation, string errorType);
}
