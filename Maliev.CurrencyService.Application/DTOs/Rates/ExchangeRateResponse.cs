using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Maliev.CurrencyService.Application.DTOs.Rates;

/// <summary>
/// Exchange rate response DTO for live and snapshot queries.
/// </summary>
/// <remarks>
/// Per contracts/openapi.yaml: Provides exchange rate data with provider tracking,
/// transitive calculation metadata, and staleness information.
/// </remarks>
public class ExchangeRateResponse
{
    /// <summary>Gets or sets the source currency code (ISO 4217).</summary>
    [Required]
    [RegularExpression("^[A-Z]{3}$")]
    [JsonPropertyName("fromCurrency")]
    public required string FromCurrency { get; init; }

    /// <summary>Gets or sets the target currency code (ISO 4217).</summary>
    [Required]
    [RegularExpression("^[A-Z]{3}$")]
    [JsonPropertyName("toCurrency")]
    public required string ToCurrency { get; init; }

    /// <summary>Gets or sets the exchange rate with 6 decimal precision.</summary>
    [Required]
    public required decimal Rate { get; init; }

    /// <summary>Gets or sets the UTC timestamp when this rate was fetched.</summary>
    [Required]
    public required DateTime Timestamp { get; init; }

    /// <summary>Gets or sets the data source (e.g., "Fawazahmed", "Frankfurter", "Snapshot", "Transitive:...").</summary>
    [Required]
    [JsonPropertyName("source")]
    public required string Source { get; init; }

    /// <summary>Gets or sets a value indicating whether the rate is computed via an intermediary currency.</summary>
    [Required]
    public required bool IsTransitive { get; init; }

    /// <summary>Gets or sets the intermediary currency code if transitive (e.g., "USD").</summary>
    [RegularExpression("^[A-Z]{3}$")]
    public string? IntermediateCurrency { get; init; }

    /// <summary>Gets or sets the calculation details if transitive (e.g., "USD/THB × THB/EUR").</summary>
    public string? CalculationDetails { get; init; }

    /// <summary>Gets or sets the query mode ("live" or "snapshot").</summary>
    [Required]
    public required string Mode { get; init; }

    /// <summary>Gets or sets the snapshot date for snapshot mode (null for live mode).</summary>
    public DateOnly? SnapshotDate { get; init; }
}
