using System.Text.Json.Serialization;

namespace Maliev.CurrencyService.Api.Models.ApiResponses;

/// <summary>
/// Model representing the response from open exchange rate APIs like Frankfurter or Fawazahmed
/// </summary>
public class OpenRatesModel
{
    [JsonPropertyName("date")]
    public string? Date { get; set; }
    
    [JsonPropertyName("base")]
    public string? Base { get; set; }
    
    [JsonPropertyName("rates")]
    public Dictionary<string, decimal> Rates { get; set; } = new();
}