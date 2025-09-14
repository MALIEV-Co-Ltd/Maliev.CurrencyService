using System.ComponentModel.DataAnnotations;

namespace Maliev.CurrencyService.Api.Models;

public class ExchangeRateOptions
{
    public const string SectionName = "ExchangeRate";

    [Range(1, 1440, ErrorMessage = "CacheDurationMinutes must be between 1 and 1440 minutes")]
    public int CacheDurationMinutes { get; set; } = 30;

    [Range(1, 60, ErrorMessage = "RetryAttempts must be between 1 and 60")]
    public int RetryAttempts { get; set; } = 3;

    [Range(1, 300, ErrorMessage = "TimeoutSeconds must be between 1 and 300 seconds")]
    public int TimeoutSeconds { get; set; } = 30;

    public string FrankfurterApiUrl { get; set; } = "https://api.frankfurter.app/";

    public List<string> ProviderOrder { get; set; } = new()
    {
        "Frankfurter",
        "Fawazahmed",
        "ExchangeRateHost",
        "ExchangeRateApi"
    };
}