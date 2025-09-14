using Maliev.CurrencyService.Api.Models;

namespace Maliev.CurrencyService.Api.Services;

public interface IExchangeRateService
{
    Task<ExchangeRateDto?> GetExchangeRateAsync(string fromCurrency, string toCurrency, CancellationToken cancellationToken = default);
    Task<Dictionary<string, ExchangeRateDto>> GetMultipleRatesAsync(string baseCurrency, IEnumerable<string> targetCurrencies, CancellationToken cancellationToken = default);
    Task<ConvertCurrencyResponse?> ConvertCurrencyAsync(ConvertCurrencyRequest request, CancellationToken cancellationToken = default);
    
    // For testing purposes
    Dictionary<string, ProviderMetrics> GetProviderMetrics();
}