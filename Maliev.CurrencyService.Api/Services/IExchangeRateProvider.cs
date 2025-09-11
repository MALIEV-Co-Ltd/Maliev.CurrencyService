using Maliev.CurrencyService.Api.Models;

namespace Maliev.CurrencyService.Api.Services;

public interface IExchangeRateProvider
{
    string Name { get; }
    Task<ExchangeRateDto?> GetExchangeRateAsync(string fromCurrency, string toCurrency, CancellationToken cancellationToken = default);
    Task<Dictionary<string, ExchangeRateDto>?> GetMultipleRatesAsync(string baseCurrency, IEnumerable<string> targetCurrencies, CancellationToken cancellationToken = default);
}