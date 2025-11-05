using Maliev.CurrencyService.Api.Metrics;
using System.Diagnostics;

namespace Maliev.CurrencyService.Api.Middleware;

/// <summary>
/// Middleware to track HTTP request metrics
/// </summary>
public class MetricsMiddleware
{
    private readonly RequestDelegate _next;
    private readonly CurrencyServiceMetrics _metrics;
    private readonly ILogger<MetricsMiddleware> _logger;

    public MetricsMiddleware(
        RequestDelegate next,
        CurrencyServiceMetrics metrics,
        ILogger<MetricsMiddleware> logger)
    {
        _next = next;
        _metrics = metrics;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var stopwatch = Stopwatch.StartNew();
        var path = context.Request.Path.Value ?? "/";
        var method = context.Request.Method;

        try
        {
            await _next(context);

            stopwatch.Stop();

            // Track HTTP request metrics
            var statusCode = context.Response.StatusCode.ToString();
            _metrics.HttpRequests.WithLabels(path, method, statusCode).Inc();
            _metrics.HttpRequestDuration.WithLabels(path, method).Observe(stopwatch.Elapsed.TotalSeconds);
            _metrics.TotalRequests.WithLabels(path, method).Inc();
            _metrics.RequestDuration.WithLabels(path, method).Observe(stopwatch.Elapsed.TotalSeconds);

            _logger.LogDebug("HTTP {Method} {Path} completed in {Elapsed}ms with status {StatusCode}",
                method, path, stopwatch.ElapsedMilliseconds, statusCode);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _metrics.FailedRequests.WithLabels(path, "exception").Inc();
            _logger.LogError(ex, "HTTP {Method} {Path} failed after {Elapsed}ms",
                method, path, stopwatch.ElapsedMilliseconds);
            throw;
        }
    }
}

/// <summary>
/// Extension methods for MetricsMiddleware
/// </summary>
public static class MetricsMiddlewareExtensions
{
    public static IApplicationBuilder UseRequestMetrics(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<MetricsMiddleware>();
    }
}
