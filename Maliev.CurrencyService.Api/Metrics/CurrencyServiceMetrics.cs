using System.Diagnostics.Metrics;
using Maliev.CurrencyService.Data.Interceptors;

namespace Maliev.CurrencyService.Api.Metrics;

/// <summary>
/// OpenTelemetry metrics for Currency Service monitoring
/// </summary>
/// <remarks>
/// Per Constitution Principle XI and FR-052: Provides comprehensive metrics for
/// request rates, provider latency, provider error rates, cache hit/miss ratio,
/// and background job status.
/// </remarks>
public class CurrencyServiceMetrics : IDisposable, IDatabaseMetrics
{
    private readonly Meter _meter;
    private readonly string _environment;

    // Counters
    private readonly Counter<long> _cacheRequests;
    private readonly Counter<long> _cacheHits;
    private readonly Counter<long> _cacheMisses;
    private readonly Counter<long> _cacheInvalidationFailures;
    private readonly Counter<long> _providerCalls;
    private readonly Counter<long> _providerRequests;
    private readonly Counter<long> _providerErrors;
    private readonly Counter<long> _providerFallbacks;
    private readonly Counter<long> _httpRequests;
    private readonly Counter<long> _totalRequests;
    private readonly Counter<long> _failedRequests;
    private readonly Counter<long> _backgroundJobExecutions;
    private readonly Counter<long> _backgroundJobFailures;
    private readonly Counter<long> _snapshotBatchesProcessed;
    private readonly Counter<long> _snapshotValidationErrors;
    private readonly Counter<long> _snapshotRecordsIngested;
    private readonly Counter<long> _databaseErrors;
    private readonly Counter<long> _databaseQueries;

    // Histograms
    private readonly Histogram<double> _providerCallDuration;
    private readonly Histogram<double> _providerLatency;
    private readonly Histogram<double> _httpRequestDuration;
    private readonly Histogram<double> _requestDuration;
    private readonly Histogram<double> _backgroundJobDuration;
    private readonly Histogram<double> _snapshotBatchSize;
    private readonly Histogram<double> _databaseQueryDuration;

    // Gauges (using tracked state)
    private long _cacheSizeBytes;
    private readonly Dictionary<string, long> _lastBackgroundJobTimestamps = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="CurrencyServiceMetrics"/> class.
    /// </summary>
    /// <param name="configuration">The application configuration.</param>
    public CurrencyServiceMetrics(IConfiguration configuration)
    {
        _environment = configuration["ASPNETCORE_ENVIRONMENT"] ?? "Production";
        _meter = new Meter("currencies-meter", "1.0.0");

        // Initialize counters
        _cacheRequests = _meter.CreateCounter<long>(
            "cache_requests_total",
            description: "Total number of cache requests");

        _cacheHits = _meter.CreateCounter<long>(
            "currency.cache_hits_total",
            description: "Total number of cache hits");

        _cacheMisses = _meter.CreateCounter<long>(
            "currency.cache_misses_total",
            description: "Total number of cache misses");

        _cacheInvalidationFailures = _meter.CreateCounter<long>(
            "currency.cache_invalidation_failures_total",
            description: "Total number of cache invalidation failures");

        _providerCalls = _meter.CreateCounter<long>(
            "provider_calls_total",
            description: "Total number of calls to external providers");

        _providerRequests = _meter.CreateCounter<long>(
            "currency.provider_requests_total",
            description: "Total number of requests to external providers");

        _providerErrors = _meter.CreateCounter<long>(
            "currency.provider_errors_total",
            description: "Total number of provider errors");

        _providerFallbacks = _meter.CreateCounter<long>(
            "currency.provider_fallbacks_total",
            description: "Total number of provider fallback occurrences");

        _httpRequests = _meter.CreateCounter<long>(
            "http_requests_total",
            description: "Total number of HTTP requests");

        _totalRequests = _meter.CreateCounter<long>(
            "currency.requests_total",
            description: "Total number of requests");

        _failedRequests = _meter.CreateCounter<long>(
            "currency.requests_failed_total",
            description: "Total number of failed requests");

        _backgroundJobExecutions = _meter.CreateCounter<long>(
            "currency.background_job_executions_total",
            description: "Total number of background job executions");

        _backgroundJobFailures = _meter.CreateCounter<long>(
            "currency.background_job_failures_total",
            description: "Total number of background job failures");

        _snapshotBatchesProcessed = _meter.CreateCounter<long>(
            "currency.snapshot_batches_processed_total",
            description: "Total number of snapshot batches processed");

        _snapshotValidationErrors = _meter.CreateCounter<long>(
            "currency.snapshot_validation_errors_total",
            description: "Total number of snapshot validation errors");

        _snapshotRecordsIngested = _meter.CreateCounter<long>(
            "currency.snapshot_records_ingested_total",
            description: "Total number of snapshot records successfully ingested");

        _databaseErrors = _meter.CreateCounter<long>(
            "currency.database_errors_total",
            description: "Total number of database errors");

        _databaseQueries = _meter.CreateCounter<long>(
            "currency.database_queries_total",
            description: "Total number of database queries executed");

        // Initialize histograms
        _providerCallDuration = _meter.CreateHistogram<double>(
            "provider_call_duration_seconds",
            unit: "s",
            description: "Duration of provider API calls in seconds");

        _providerLatency = _meter.CreateHistogram<double>(
            "currency.provider_latency_seconds",
            unit: "s",
            description: "Latency of provider API calls in seconds");

        _httpRequestDuration = _meter.CreateHistogram<double>(
            "http_request_duration_seconds",
            unit: "s",
            description: "Duration of HTTP requests in seconds");

        _requestDuration = _meter.CreateHistogram<double>(
            "currency.request_duration_seconds",
            unit: "s",
            description: "Duration of HTTP requests in seconds");

        _backgroundJobDuration = _meter.CreateHistogram<double>(
            "currency.background_job_duration_seconds",
            unit: "s",
            description: "Duration of background job executions in seconds");

        _snapshotBatchSize = _meter.CreateHistogram<double>(
            "currency.snapshot_batch_size",
            description: "Size of snapshot batches");

        _databaseQueryDuration = _meter.CreateHistogram<double>(
            "currency.database_query_duration_seconds",
            unit: "s",
            description: "Duration of database queries in seconds");

        // Initialize observable gauges
        _meter.CreateObservableGauge(
            "currency.cache_size_bytes",
            () => new Measurement<long>(_cacheSizeBytes),
            description: "Current size of cache in bytes");

        _meter.CreateObservableGauge(
            "currency.background_job_last_run_timestamp",
            () =>
            {
                var measurements = new List<Measurement<long>>();
                lock (_lastBackgroundJobTimestamps)
                {
                    foreach (var kvp in _lastBackgroundJobTimestamps)
                    {
                        measurements.Add(new Measurement<long>(kvp.Value,
                            new KeyValuePair<string, object?>("job_name", kvp.Key)));
                    }
                }
                return measurements;
            },
            description: "Unix timestamp of last background job execution");
    }

    // Cache metrics methods
    /// <summary>
    /// Records a cache request with the specified result.
    /// </summary>
    /// <param name="result">The result type (hit/miss).</param>
    public void RecordCacheRequest(string result)
    {
        _cacheRequests.Add(1, new KeyValuePair<string, object?>("result", result));
    }

    /// <summary>
    /// Records a cache hit.
    /// </summary>
    public void RecordCacheHit()
    {
        _cacheHits.Add(1);
    }

    /// <summary>
    /// Records a cache miss.
    /// </summary>
    public void RecordCacheMiss()
    {
        _cacheMisses.Add(1);
    }

    /// <summary>
    /// Records a cache invalidation failure.
    /// </summary>
    public void RecordCacheInvalidationFailure()
    {
        _cacheInvalidationFailures.Add(1);
    }

    /// <summary>
    /// Sets the current cache size in bytes.
    /// </summary>
    /// <param name="bytes">The cache size in bytes.</param>
    public void SetCacheSizeBytes(long bytes)
    {
        Interlocked.Exchange(ref _cacheSizeBytes, bytes);
    }

    // Provider metrics methods
    /// <summary>
    /// Records a provider call with the specified status.
    /// </summary>
    /// <param name="provider">The provider name.</param>
    /// <param name="status">The call status (success/error).</param>
    public void RecordProviderCall(string provider, string status)
    {
        _providerCalls.Add(1,
            new KeyValuePair<string, object?>("provider", provider),
            new KeyValuePair<string, object?>("status", status));
    }

    /// <summary>
    /// Records the duration of a provider call.
    /// </summary>
    /// <param name="provider">The provider name.</param>
    /// <param name="durationSeconds">The call duration in seconds.</param>
    public void RecordProviderCallDuration(string provider, double durationSeconds)
    {
        _providerCallDuration.Record(durationSeconds,
            new KeyValuePair<string, object?>("provider", provider));
    }

    /// <summary>
    /// Records a request to an external provider.
    /// </summary>
    /// <param name="provider">The provider name.</param>
    /// <param name="currencyPair">The currency pair requested.</param>
    public void RecordProviderRequest(string provider, string currencyPair)
    {
        _providerRequests.Add(1,
            new KeyValuePair<string, object?>("provider", provider),
            new KeyValuePair<string, object?>("currency_pair", currencyPair));
    }

    /// <summary>
    /// Records a provider error.
    /// </summary>
    /// <param name="provider">The provider name.</param>
    /// <param name="errorType">The error type.</param>
    public void RecordProviderError(string provider, string errorType)
    {
        _providerErrors.Add(1,
            new KeyValuePair<string, object?>("provider", provider),
            new KeyValuePair<string, object?>("error_type", errorType));
    }

    /// <summary>
    /// Records the latency of a provider call.
    /// </summary>
    /// <param name="provider">The provider name.</param>
    /// <param name="latencySeconds">The latency in seconds.</param>
    public void RecordProviderLatency(string provider, double latencySeconds)
    {
        _providerLatency.Record(latencySeconds,
            new KeyValuePair<string, object?>("provider", provider));
    }

    /// <summary>
    /// Records a provider fallback occurrence.
    /// </summary>
    /// <param name="fromProvider">The provider that failed.</param>
    /// <param name="toProvider">The provider that was used instead.</param>
    public void RecordProviderFallback(string fromProvider, string toProvider)
    {
        _providerFallbacks.Add(1,
            new KeyValuePair<string, object?>("from_provider", fromProvider),
            new KeyValuePair<string, object?>("to_provider", toProvider));
    }

    // Request metrics methods
    /// <summary>
    /// Records an HTTP request.
    /// </summary>
    /// <param name="endpoint">The endpoint path.</param>
    /// <param name="method">The HTTP method.</param>
    /// <param name="status">The HTTP status code.</param>
    public void RecordHttpRequest(string endpoint, string method, string status)
    {
        _httpRequests.Add(1,
            new KeyValuePair<string, object?>("endpoint", endpoint),
            new KeyValuePair<string, object?>("method", method),
            new KeyValuePair<string, object?>("status", status));
    }

    /// <summary>
    /// Records the duration of an HTTP request.
    /// </summary>
    /// <param name="endpoint">The endpoint path.</param>
    /// <param name="method">The HTTP method.</param>
    /// <param name="durationSeconds">The request duration in seconds.</param>
    public void RecordHttpRequestDuration(string endpoint, string method, double durationSeconds)
    {
        _httpRequestDuration.Record(durationSeconds,
            new KeyValuePair<string, object?>("endpoint", endpoint),
            new KeyValuePair<string, object?>("method", method));
    }

    /// <summary>
    /// Records a total request.
    /// </summary>
    /// <param name="endpoint">The endpoint path.</param>
    /// <param name="method">The HTTP method.</param>
    public void RecordTotalRequest(string endpoint, string method)
    {
        _totalRequests.Add(1,
            new KeyValuePair<string, object?>("endpoint", endpoint),
            new KeyValuePair<string, object?>("method", method));
    }

    /// <summary>
    /// Records a failed request.
    /// </summary>
    /// <param name="endpoint">The endpoint path.</param>
    /// <param name="errorType">The error type.</param>
    public void RecordFailedRequest(string endpoint, string errorType)
    {
        _failedRequests.Add(1,
            new KeyValuePair<string, object?>("endpoint", endpoint),
            new KeyValuePair<string, object?>("error_type", errorType));
    }

    /// <summary>
    /// Records the duration of a request.
    /// </summary>
    /// <param name="endpoint">The endpoint path.</param>
    /// <param name="method">The HTTP method.</param>
    /// <param name="durationSeconds">The request duration in seconds.</param>
    public void RecordRequestDuration(string endpoint, string method, double durationSeconds)
    {
        _requestDuration.Record(durationSeconds,
            new KeyValuePair<string, object?>("endpoint", endpoint),
            new KeyValuePair<string, object?>("method", method));
    }

    // Background job metrics methods
    /// <summary>
    /// Records the execution of a background job.
    /// </summary>
    /// <param name="jobName">The job name.</param>
    public void RecordBackgroundJobExecution(string jobName)
    {
        _backgroundJobExecutions.Add(1,
            new KeyValuePair<string, object?>("job_name", jobName));
    }

    /// <summary>
    /// Records a background job failure.
    /// </summary>
    /// <param name="jobName">The job name.</param>
    /// <param name="errorType">The error type.</param>
    public void RecordBackgroundJobFailure(string jobName, string errorType)
    {
        _backgroundJobFailures.Add(1,
            new KeyValuePair<string, object?>("job_name", jobName),
            new KeyValuePair<string, object?>("error_type", errorType));
    }

    /// <summary>
    /// Records the duration of a background job execution.
    /// </summary>
    /// <param name="jobName">The job name.</param>
    /// <param name="durationSeconds">The job duration in seconds.</param>
    public void RecordBackgroundJobDuration(string jobName, double durationSeconds)
    {
        _backgroundJobDuration.Record(durationSeconds,
            new KeyValuePair<string, object?>("job_name", jobName));
    }

    /// <summary>
    /// Sets the last execution timestamp for a background job.
    /// </summary>
    /// <param name="jobName">The job name.</param>
    /// <param name="timestamp">The Unix timestamp of the last execution.</param>
    public void SetLastBackgroundJobTimestamp(string jobName, long timestamp)
    {
        lock (_lastBackgroundJobTimestamps)
        {
            _lastBackgroundJobTimestamps[jobName] = timestamp;
        }
    }

    // Snapshot processing metrics methods
    /// <summary>
    /// Records the processing of a snapshot batch.
    /// </summary>
    public void RecordSnapshotBatchProcessed()
    {
        _snapshotBatchesProcessed.Add(1);
    }

    /// <summary>
    /// Records the size of a snapshot batch.
    /// </summary>
    /// <param name="size">The batch size.</param>
    public void RecordSnapshotBatchSize(double size)
    {
        _snapshotBatchSize.Record(size);
    }

    /// <summary>
    /// Records a snapshot validation error.
    /// </summary>
    /// <param name="errorType">The error type.</param>
    public void RecordSnapshotValidationError(string errorType)
    {
        _snapshotValidationErrors.Add(1,
            new KeyValuePair<string, object?>("error_type", errorType));
    }

    /// <summary>
    /// Records the number of snapshot records ingested.
    /// </summary>
    /// <param name="count">The number of records.</param>
    public void RecordSnapshotRecordsIngested(long count)
    {
        _snapshotRecordsIngested.Add(count);
    }

    // Database metrics methods
    /// <summary>
    /// Records the duration of a database query.
    /// </summary>
    /// <param name="operation">The database operation.</param>
    /// <param name="durationSeconds">The query duration in seconds.</param>
    public void RecordDatabaseQueryDuration(string operation, double durationSeconds)
    {
        _databaseQueryDuration.Record(durationSeconds,
            new KeyValuePair<string, object?>("operation", operation));
    }

    /// <summary>
    /// Records a database error.
    /// </summary>
    /// <param name="operation">The database operation.</param>
    /// <param name="errorType">The error type.</param>
    public void RecordDatabaseError(string operation, string errorType)
    {
        _databaseErrors.Add(1,
            new KeyValuePair<string, object?>("operation", operation),
            new KeyValuePair<string, object?>("error_type", errorType));
    }

    /// <summary>
    /// Records a database query execution.
    /// </summary>
    /// <param name="operation">The database operation (SELECT, INSERT, etc).</param>
    public void RecordDatabaseQuery(string operation)
    {
        _databaseQueries.Add(1,
            new KeyValuePair<string, object?>("operation", operation));
    }

    /// <summary>
    /// Releases all resources associated with the metrics.
    /// </summary>
    public void Dispose()
    {
        _meter?.Dispose();
        GC.SuppressFinalize(this);
    }
}
