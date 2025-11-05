using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Maliev.CurrencyService.Api.Models.Rates;

/// <summary>
/// Exchange rate response DTO for live and snapshot queries
/// </summary>
/// <remarks>
/// Per contracts/openapi.yaml: Provides exchange rate data with provider tracking,
/// transitive calculation metadata, and staleness information.
/// </remarks>
public class ExchangeRateResponse
{
    /// <summary>
    /// Source currency code (ISO 4217)
    /// </summary>
    [Required]
    [RegularExpression("^[A-Z]{3}$")]
    [JsonPropertyName("fromCurrency")]
    public required string FromCurrency { get; init; }

    /// <summary>
    /// Target currency code (ISO 4217)
    /// </summary>
    [Required]
    [RegularExpression("^[A-Z]{3}$")]
    [JsonPropertyName("toCurrency")]
    public required string ToCurrency { get; init; }

    /// <summary>
    /// Exchange rate with 6 decimal precision
    /// </summary>
    [Required]
    public required decimal Rate { get; init; }

    /// <summary>
    /// Rate timestamp (UTC)
    /// </summary>
    [Required]
    public required DateTime Timestamp { get; init; }

    /// <summary>
    /// Data source (Fawazahmed, Frankfurter, Snapshot, or Transitive:...)
    /// </summary>
    [Required]
    [JsonPropertyName("source")]
    public required string Source { get; init; }

    /// <summary>
    /// Whether rate is computed via intermediary currency
    /// </summary>
    [Required]
    public required bool IsTransitive { get; init; }

    /// <summary>
    /// Intermediary currency code if transitive (e.g., "USD")
    /// </summary>
    [RegularExpression("^[A-Z]{3}$")]
    public string? IntermediateCurrency { get; init; }

    /// <summary>
    /// Calculation details if transitive (e.g., "USD/THB × THB/EUR")
    /// </summary>
    public string? CalculationDetails { get; init; }

    /// <summary>
    /// Query mode (live or snapshot)
    /// </summary>
    [Required]
    public required string Mode { get; init; }

    /// <summary>
    /// Snapshot date for snapshot mode (null for live mode)
    /// </summary>
    public DateOnly? SnapshotDate { get; init; }
}
