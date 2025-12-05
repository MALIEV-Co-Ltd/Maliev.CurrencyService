using System.Text.Json.Serialization;

namespace Maliev.CurrencyService.Api.Models.ApiResponses;

/// <summary>
/// Model representing the response from open exchange rate APIs like Frankfurter or Fawazahmed
/// </summary>
public class OpenRatesModel
{
    /// <summary>
    /// The date for which the exchange rates are provided.
    /// </summary>
    [JsonPropertyName("date")]
    public string? Date { get; set; }
    
    /// <summary>
    /// The base currency for the exchange rates.
    /// </summary>
    [JsonPropertyName("base")]
    public string? Base { get; set; }
    
    /// <summary>
    /// A dictionary of exchange rates where the key is the target currency code and the value is the rate.
    /// </summary>
    [JsonPropertyName("rates")]
    public Dictionary<string, decimal> Rates { get; set; } = new();
}