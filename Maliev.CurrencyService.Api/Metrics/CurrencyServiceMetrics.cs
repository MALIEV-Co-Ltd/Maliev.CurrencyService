using Prometheus;

namespace Maliev.CurrencyService.Api.Metrics;

/// <summary>
/// Prometheus metrics for Currency Service monitoring
/// </summary>
/// <remarks>
/// Per Constitution Principle XI and FR-052: Provides comprehensive metrics for
/// request rates, provider latency, provider error rates, cache hit/miss ratio,
/// and background job status.
/// </remarks>
public class CurrencyServiceMetrics
{
    // Cache metrics (with labels for test compatibility)
    public Counter CacheRequests { get; }  // cache_requests_total{result="hit|miss"}
    public Counter CacheHits { get; }
    public Counter CacheMisses { get; }
    public Counter CacheInvalidationFailures { get; }
    public Gauge CacheSizeBytes { get; }

    // Provider metrics (with labels for test compatibility)
    public Counter ProviderCalls { get; }  // provider_calls_total{provider="...",status="success|error"}
    public Histogram ProviderCallDuration { get; }  // provider_call_duration_seconds{provider="..."}
    public Counter ProviderRequests { get; }
    public Counter ProviderErrors { get; }
    public Histogram ProviderLatency { get; }
    public Counter ProviderFallbacks { get; }

    // Request metrics (with labels for test compatibility)
    public Counter HttpRequests { get; }  // http_requests_total{endpoint="..."}
    public Histogram HttpRequestDuration { get; }  // http_request_duration_seconds{endpoint="...",method="..."}
    public Counter TotalRequests { get; }
    public Counter FailedRequests { get; }
    public Histogram RequestDuration { get; }

    // Background job metrics
    public Counter BackgroundJobExecutions { get; }
    public Counter BackgroundJobFailures { get; }
    public Histogram BackgroundJobDuration { get; }
    public Gauge LastBackgroundJobTimestamp { get; }

    // Snapshot processing metrics (US4)
    public Counter SnapshotBatchesProcessed { get; }
    public Histogram SnapshotBatchSize { get; }
    public Counter SnapshotValidationErrors { get; }
    public Counter SnapshotRecordsIngested { get; }

    // Database metrics
    public Histogram DatabaseQueryDuration { get; }
    public Counter DatabaseErrors { get; }

    public CurrencyServiceMetrics()
    {
        // Cache metrics (test-compatible format)
        CacheRequests = Prometheus.Metrics.CreateCounter(
            "cache_requests_total",
            "Total number of cache requests",
            new CounterConfiguration
            {
                LabelNames = new[] { "result" }
            });

        CacheHits = Prometheus.Metrics.CreateCounter(
            "currency_cache_hits_total",
            "Total number of cache hits");

        CacheMisses = Prometheus.Metrics.CreateCounter(
            "currency_cache_misses_total",
            "Total number of cache misses");

        CacheInvalidationFailures = Prometheus.Metrics.CreateCounter(
            "currency_cache_invalidation_failures_total",
            "Total number of cache invalidation failures");

        CacheSizeBytes = Prometheus.Metrics.CreateGauge(
            "currency_cache_size_bytes",
            "Current size of cache in bytes");

        // Provider metrics (test-compatible format)
        ProviderCalls = Prometheus.Metrics.CreateCounter(
            "provider_calls_total",
            "Total number of calls to external providers",
            new CounterConfiguration
            {
                LabelNames = new[] { "provider", "status" }
            });

        ProviderCallDuration = Prometheus.Metrics.CreateHistogram(
            "provider_call_duration_seconds",
            "Duration of provider API calls in seconds",
            new HistogramConfiguration
            {
                LabelNames = new[] { "provider" },
                Buckets = new[] { 0.01, 0.05, 0.1, 0.25, 0.5, 1.0, 2.5, 5.0 }
            });

        ProviderRequests = Prometheus.Metrics.CreateCounter(
            "currency_provider_requests_total",
            "Total number of requests to external providers",
            new CounterConfiguration
            {
                LabelNames = new[] { "provider", "currency_pair" }
            });

        ProviderErrors = Prometheus.Metrics.CreateCounter(
            "currency_provider_errors_total",
            "Total number of provider errors",
            new CounterConfiguration
            {
                LabelNames = new[] { "provider", "error_type" }
            });

        ProviderLatency = Prometheus.Metrics.CreateHistogram(
            "currency_provider_latency_seconds",
            "Latency of provider API calls in seconds",
            new HistogramConfiguration
            {
                LabelNames = new[] { "provider" },
                Buckets = new[] { 0.01, 0.05, 0.1, 0.25, 0.5, 1.0, 2.5, 5.0 }
            });

        ProviderFallbacks = Prometheus.Metrics.CreateCounter(
            "currency_provider_fallbacks_total",
            "Total number of provider fallback occurrences",
            new CounterConfiguration
            {
                LabelNames = new[] { "from_provider", "to_provider" }
            });

        // Request metrics (test-compatible format)
        HttpRequests = Prometheus.Metrics.CreateCounter(
            "http_requests_total",
            "Total number of HTTP requests",
            new CounterConfiguration
            {
                LabelNames = new[] { "endpoint", "method", "status" }
            });

        HttpRequestDuration = Prometheus.Metrics.CreateHistogram(
            "http_request_duration_seconds",
            "Duration of HTTP requests in seconds",
            new HistogramConfiguration
            {
                LabelNames = new[] { "endpoint", "method" },
                Buckets = new[] { 0.001, 0.005, 0.01, 0.025, 0.05, 0.1, 0.25, 0.5, 1.0 }
            });

        TotalRequests = Prometheus.Metrics.CreateCounter(
            "currency_requests_total",
            "Total number of requests",
            new CounterConfiguration
            {
                LabelNames = new[] { "endpoint", "method" }
            });

        FailedRequests = Prometheus.Metrics.CreateCounter(
            "currency_requests_failed_total",
            "Total number of failed requests",
            new CounterConfiguration
            {
                LabelNames = new[] { "endpoint", "error_type" }
            });

        RequestDuration = Prometheus.Metrics.CreateHistogram(
            "currency_request_duration_seconds",
            "Duration of HTTP requests in seconds",
            new HistogramConfiguration
            {
                LabelNames = new[] { "endpoint", "method" },
                Buckets = new[] { 0.001, 0.005, 0.01, 0.025, 0.05, 0.1, 0.25, 0.5, 1.0 }
            });

        // Background job metrics
        BackgroundJobExecutions = Prometheus.Metrics.CreateCounter(
            "currency_background_job_executions_total",
            "Total number of background job executions",
            new CounterConfiguration
            {
                LabelNames = new[] { "job_name" }
            });

        BackgroundJobFailures = Prometheus.Metrics.CreateCounter(
            "currency_background_job_failures_total",
            "Total number of background job failures",
            new CounterConfiguration
            {
                LabelNames = new[] { "job_name", "error_type" }
            });

        BackgroundJobDuration = Prometheus.Metrics.CreateHistogram(
            "currency_background_job_duration_seconds",
            "Duration of background job executions in seconds",
            new HistogramConfiguration
            {
                LabelNames = new[] { "job_name" },
                Buckets = new[] { 1.0, 5.0, 10.0, 30.0, 60.0, 120.0, 300.0, 600.0 }
            });

        LastBackgroundJobTimestamp = Prometheus.Metrics.CreateGauge(
            "currency_background_job_last_run_timestamp",
            "Unix timestamp of last background job execution",
            new GaugeConfiguration
            {
                LabelNames = new[] { "job_name" }
            });

        // Snapshot processing metrics
        SnapshotBatchesProcessed = Prometheus.Metrics.CreateCounter(
            "currency_snapshot_batches_processed_total",
            "Total number of snapshot batches processed");

        SnapshotBatchSize = Prometheus.Metrics.CreateHistogram(
            "currency_snapshot_batch_size",
            "Size of snapshot batches",
            new HistogramConfiguration
            {
                Buckets = new[] { 10.0, 50.0, 100.0, 500.0, 1000.0, 5000.0, 10000.0 }
            });

        SnapshotValidationErrors = Prometheus.Metrics.CreateCounter(
            "currency_snapshot_validation_errors_total",
            "Total number of snapshot validation errors",
            new CounterConfiguration
            {
                LabelNames = new[] { "error_type" }
            });

        SnapshotRecordsIngested = Prometheus.Metrics.CreateCounter(
            "currency_snapshot_records_ingested_total",
            "Total number of snapshot records successfully ingested");

        // Database metrics
        DatabaseQueryDuration = Prometheus.Metrics.CreateHistogram(
            "currency_database_query_duration_seconds",
            "Duration of database queries in seconds",
            new HistogramConfiguration
            {
                LabelNames = new[] { "operation" },
                Buckets = new[] { 0.001, 0.005, 0.01, 0.025, 0.05, 0.1, 0.25, 0.5 }
            });

        DatabaseErrors = Prometheus.Metrics.CreateCounter(
            "currency_database_errors_total",
            "Total number of database errors",
            new CounterConfiguration
            {
                LabelNames = new[] { "operation", "error_type" }
            });
    }
}
