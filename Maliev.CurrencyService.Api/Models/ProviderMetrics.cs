using System.ComponentModel.DataAnnotations;

namespace Maliev.CurrencyService.Api.Models;

/// <summary>
/// Represents metrics for a single exchange rate provider.
/// </summary>
public class ProviderMetrics
{
    /// <summary>
    /// The name of the exchange rate provider.
    /// </summary>
    public string ProviderName { get; set; } = string.Empty;
    /// <summary>
    /// The total number of requests made to the provider.
    /// </summary>
    public int TotalRequests { get; set; }
    /// <summary>
    /// The number of successful requests made to the provider.
    /// </summary>
    public int SuccessfulRequests { get; set; }
    /// <summary>
    /// The total response time in milliseconds for all requests to the provider.
    /// </summary>
    public long TotalResponseTimeMs { get; set; }
    /// <summary>
    /// The timestamp of the last request made to the provider.
    /// </summary>
    public DateTime LastRequestAt { get; set; }

    /// <summary>
    /// Calculates the success rate of the provider.
    /// </summary>
    public double SuccessRate => TotalRequests > 0 ? (double)SuccessfulRequests / TotalRequests : 0;
    /// <summary>
    /// Calculates the average response time in milliseconds for the provider.
    /// </summary>
    public double AverageResponseTimeMs => TotalRequests > 0 ? (double)TotalResponseTimeMs / TotalRequests : 0;
    /// <summary>
    /// The number of error requests made to the provider.
    /// </summary>
    public int ErrorRequests => TotalRequests - SuccessfulRequests;
    /// <summary>
    /// Calculates the error rate of the provider.
    /// </summary>
    public double ErrorRate => TotalRequests > 0 ? (double)ErrorRequests / TotalRequests : 0;
}