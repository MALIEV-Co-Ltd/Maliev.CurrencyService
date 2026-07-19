namespace Maliev.CurrencyService.Api.Models.Common;

/// <summary>
/// Standard error response structure for all API errors
/// </summary>
/// <remarks>
/// Per contracts/openapi.yaml: Provides consistent error structure with correlation ID for tracking.
/// </remarks>
public class ErrorResponse
{
    /// <summary>
    /// Error type (e.g., "BadRequest", "NotFound", "ServiceUnavailable")
    /// </summary>
    public required string Error { get; init; }

    /// <summary>
    /// Human-readable error message
    /// </summary>
    public required string Message { get; init; }

    /// <summary>
    /// Error timestamp (UTC)
    /// </summary>
    public required DateTime Timestamp { get; init; }

    /// <summary>
    /// Correlation ID for request tracking
    /// </summary>
    public string? CorrelationId { get; init; }

    /// <summary>
    /// Additional error details (optional, for validation errors)
    /// </summary>
    public Dictionary<string, string[]>? Details { get; init; }
}
