using System.Text.Json;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Maliev.CurrencyService.Api.HealthChecks;

/// <summary>
/// Custom health check response writer that outputs JSON in the expected format
/// </summary>
public static class HealthCheckResponseWriter
{
    /// <summary>
    /// Writes the health check response as a detailed JSON object.
    /// </summary>
    /// <param name="context">The <see cref="HttpContext"/> for the request.</param>
    /// <param name="report">The <see cref="HealthReport"/> containing the health check results.</param>
    /// <returns>A <see cref="Task"/> that represents the asynchronous write operation.</returns>
    public static Task WriteResponse(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json";

        var result = new
        {
            Status = report.Status.ToString(),
            Checks = report.Entries.ToDictionary(
                kvp => kvp.Key,
                kvp => new
                {
                    Status = kvp.Value.Status.ToString(),
                    Description = kvp.Value.Description ?? string.Empty,
                    Duration = kvp.Value.Duration
                }),
            TotalDuration = report.TotalDuration
        };

        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = null, // Use exact property names (PascalCase)
            WriteIndented = true
        };

        return context.Response.WriteAsJsonAsync(result, options);
    }
}
