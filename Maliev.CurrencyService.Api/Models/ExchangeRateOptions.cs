using System.ComponentModel.DataAnnotations;

namespace Maliev.CurrencyService.Api.Models;

/// <summary>
/// Configuration options for exchange rate services.
/// </summary>
public class ExchangeRateOptions
{
    /// <summary>
    /// The name of the configuration section for exchange rate options.
    /// </summary>
    public const string SectionName = "ExchangeRate";

    /// <summary>
    /// The duration in minutes for which exchange rates are cached.
    /// </summary>
    [Range(1, 1440, ErrorMessage = "CacheDurationMinutes must be between 1 and 1440 minutes")]
    public int CacheDurationMinutes { get; set; } = 30;

    /// <summary>
    /// The number of retry attempts for external API calls.
    /// </summary>
    [Range(1, 60, ErrorMessage = "RetryAttempts must be between 1 and 60")]
    public int RetryAttempts { get; set; } = 3;

    /// <summary>
    /// The timeout in seconds for external API calls.
    /// </summary>
    [Range(1, 300, ErrorMessage = "TimeoutSeconds must be between 1 and 300 seconds")]
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// The base URL for the Frankfurter API.
    /// </summary>
    public string FrankfurterApiUrl { get; set; } = "https://api.frankfurter.app/";

    /// <summary>
    /// The ordered list of exchange rate providers to use.
    /// </summary>
    public List<string> ProviderOrder { get; set; } = new()
    {
        "Frankfurter",
        "Fawazahmed",
        "ExchangeRateHost",
        "ExchangeRateApi"
    };

    /// <summary>
    /// Gets or sets a value indicating whether dynamic provider prioritization is enabled.
    /// </summary>
    public bool EnableDynamicPrioritization { get; set; } = false;
    
    /// <summary>
    /// Gets or sets the minimum number of requests required for a provider to be included in dynamic prioritization.
    /// </summary>
    public int MinRequestsForPrioritization { get; set; } = 10;
    
    /// <summary>
    /// Gets or sets the weight given to response time when calculating provider priority.
    /// </summary>
    public double ResponseTimeWeight { get; set; } = 0.4;
    
    /// <summary>
    /// Gets or sets the weight given to success rate when calculating provider priority.
    /// </summary>
    public double SuccessRateWeight { get; set; } = 0.3;
    
    /// <summary>
    /// Gets or sets the weight given to error rate when calculating provider priority.
    /// </summary>
    public double ErrorRateWeight { get; set; } = 0.2;
    
    /// <summary>
    /// Gets or sets the weight given to request count when calculating provider priority.
    /// </summary>
    public double RequestCountWeight { get; set; } = 0.1;
}
