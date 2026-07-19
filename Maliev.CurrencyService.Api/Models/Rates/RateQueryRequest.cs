using System.ComponentModel.DataAnnotations;

namespace Maliev.CurrencyService.Api.Models.Rates;

/// <summary>
/// Request parameters for exchange rate query
/// </summary>
/// <remarks>
/// Per contracts/openapi.yaml: Supports both live and snapshot modes.
/// User Story 2 focuses on live mode; User Story 3 adds snapshot mode.
/// </remarks>
public class RateQueryRequest
{
    /// <summary>
    /// Source currency code (ISO 4217, 3 letters)
    /// </summary>
    [Required]
    [RegularExpression("^[A-Z]{3}$", ErrorMessage = "From currency must be a 3-letter ISO 4217 code")]
    public required string From { get; init; }

    /// <summary>
    /// Target currency code (ISO 4217, 3 letters)
    /// </summary>
    [Required]
    [RegularExpression("^[A-Z]{3}$", ErrorMessage = "To currency must be a 3-letter ISO 4217 code")]
    public required string To { get; init; }

    /// <summary>
    /// Query mode: "live" for current rates, "snapshot" for historical rates
    /// </summary>
    public string Mode { get; init; } = "live";

    /// <summary>
    /// Snapshot date (required if mode=snapshot, format: YYYY-MM-DD)
    /// </summary>
    public DateOnly? Date { get; init; }
}
