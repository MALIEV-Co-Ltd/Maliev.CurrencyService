namespace Maliev.CurrencyService.Application.Interfaces;

/// <summary>
/// Interface for recording external exchange rate provider metrics.
/// </summary>
public interface IProviderMetrics
{
    /// <summary>Records a request to an external provider.</summary>
    /// <param name="provider">The provider name.</param>
    /// <param name="currencyPair">The currency pair requested.</param>
    void RecordProviderRequest(string provider, string currencyPair);

    /// <summary>Records a provider error.</summary>
    /// <param name="provider">The provider name.</param>
    /// <param name="errorType">The error type.</param>
    void RecordProviderError(string provider, string errorType);

    /// <summary>Records the latency of a provider call.</summary>
    /// <param name="provider">The provider name.</param>
    /// <param name="latencySeconds">The latency in seconds.</param>
    void RecordProviderLatency(string provider, double latencySeconds);

    /// <summary>Records the duration of a provider call.</summary>
    /// <param name="provider">The provider name.</param>
    /// <param name="durationSeconds">The call duration in seconds.</param>
    void RecordProviderCallDuration(string provider, double durationSeconds);

    /// <summary>Records a provider call with the specified status.</summary>
    /// <param name="provider">The provider name.</param>
    /// <param name="status">The call status (success/error).</param>
    void RecordProviderCall(string provider, string status);

    /// <summary>Records a provider fallback occurrence.</summary>
    /// <param name="fromProvider">The provider that failed.</param>
    /// <param name="toProvider">The provider that was used instead.</param>
    void RecordProviderFallback(string fromProvider, string toProvider);
}
