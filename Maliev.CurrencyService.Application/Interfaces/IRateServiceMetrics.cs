namespace Maliev.CurrencyService.Application.Interfaces;

/// <summary>
/// Metrics interface for the rate and snapshot service operations.
/// </summary>
public interface IRateServiceMetrics
{
    /// <summary>Records a cache request with the specified result.</summary>
    /// <param name="result">The result type (hit/miss).</param>
    void RecordCacheRequest(string result);

    /// <summary>Records a cache hit.</summary>
    void RecordCacheHit();

    /// <summary>Records a cache miss.</summary>
    void RecordCacheMiss();

    /// <summary>Records a provider error.</summary>
    /// <param name="provider">The provider name.</param>
    /// <param name="errorType">The error type.</param>
    void RecordProviderError(string provider, string errorType);

    /// <summary>Records the execution of a background job.</summary>
    /// <param name="jobName">The job name.</param>
    void RecordBackgroundJobExecution(string jobName);

    /// <summary>Records a background job failure.</summary>
    /// <param name="jobName">The job name.</param>
    /// <param name="errorType">The error type.</param>
    void RecordBackgroundJobFailure(string jobName, string errorType);

    /// <summary>Records the duration of a background job execution.</summary>
    /// <param name="jobName">The job name.</param>
    /// <param name="durationSeconds">The job duration in seconds.</param>
    void RecordBackgroundJobDuration(string jobName, double durationSeconds);
}
