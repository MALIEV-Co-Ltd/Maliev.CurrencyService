using System.Text.Json;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Maliev.CurrencyService.Api.HealthChecks;

/// <summary>
/// Custom health check response writer that outputs JSON in the expected format
/// </summary>
public static class HealthCheckResponseWriter
{
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
