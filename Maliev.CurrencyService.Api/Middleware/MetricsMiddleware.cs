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

    /// <summary>
    /// Initializes a new instance of the <see cref="MetricsMiddleware"/> class.
    /// </summary>
    /// <param name="next">The next middleware in the pipeline.</param>
    /// <param name="metrics">The application metrics service.</param>
    /// <param name="logger">The logger for this middleware.</param>
    public MetricsMiddleware(
        RequestDelegate next,
        CurrencyServiceMetrics metrics,
        ILogger<MetricsMiddleware> logger)
    {
        _next = next;
        _metrics = metrics;
        _logger = logger;
    }

    /// <summary>
    /// Invokes the middleware to capture and record HTTP request metrics.
    /// </summary>
    /// <param name="context">The <see cref="HttpContext"/> for the current request.</param>
    /// <returns>A <see cref="Task"/> that represents the execution of this middleware.</returns>
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
            _metrics.RecordHttpRequest(path, method, statusCode);
            _metrics.RecordHttpRequestDuration(path, method, stopwatch.Elapsed.TotalSeconds);
            _metrics.RecordTotalRequest(path, method);
            _metrics.RecordRequestDuration(path, method, stopwatch.Elapsed.TotalSeconds);

            _logger.LogDebug("HTTP {Method} {Path} completed in {Elapsed}ms with status {StatusCode}",
                method, path, stopwatch.ElapsedMilliseconds, statusCode);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _metrics.RecordFailedRequest(path, "exception");
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
    /// <summary>
    /// Adds the <see cref="MetricsMiddleware"/> to the application's request pipeline to track HTTP metrics.
    /// </summary>
    /// <param name="builder">The <see cref="IApplicationBuilder"/>.</param>
    /// <returns>The configured <see cref="IApplicationBuilder"/>.</returns>
    public static IApplicationBuilder UseRequestMetrics(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<MetricsMiddleware>();
    }
}
