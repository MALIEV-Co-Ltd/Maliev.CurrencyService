using System.ComponentModel.DataAnnotations;

namespace Maliev.CurrencyService.Api.Models;

public class ProviderMetrics
{
    public string ProviderName { get; set; } = string.Empty;
    public int TotalRequests { get; set; }
    public int SuccessfulRequests { get; set; }
    public long TotalResponseTimeMs { get; set; }
    public DateTime LastRequestAt { get; set; }

    public double SuccessRate => TotalRequests > 0 ? (double)SuccessfulRequests / TotalRequests : 0;
    public double AverageResponseTimeMs => TotalRequests > 0 ? (double)TotalResponseTimeMs / TotalRequests : 0;
    public int ErrorRequests => TotalRequests - SuccessfulRequests;
    public double ErrorRate => TotalRequests > 0 ? (double)ErrorRequests / TotalRequests : 0;
}