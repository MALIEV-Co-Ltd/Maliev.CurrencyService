namespace Maliev.CurrencyService.Data.Interceptors;

/// <summary>
/// Interface for recording database metrics
/// </summary>
public interface IDatabaseMetrics
{
    /// <summary>
    /// Records a database query execution.
    /// </summary>
    void RecordDatabaseQuery(string operation);

    /// <summary>
    /// Records the duration of a database query.
    /// </summary>
    void RecordDatabaseQueryDuration(string operation, double durationSeconds);

    /// <summary>
    /// Records a database error.
    /// </summary>
    void RecordDatabaseError(string operation, string errorType);
}
