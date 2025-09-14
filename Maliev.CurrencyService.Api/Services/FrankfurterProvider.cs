using Maliev.CurrencyService.Api.Models;
using Maliev.CurrencyService.Api.Models.ApiResponses;
using Maliev.CurrencyService.Api.Services.Clients;
using Microsoft.Extensions.Options;

namespace Maliev.CurrencyService.Api.Services;

public class FrankfurterProvider : IExchangeRateProvider
{
    private readonly FrankfurterApiClient _frankfurterApiClient;
    private readonly ExchangeRateOptions _options;
    private readonly ILogger<FrankfurterProvider> _logger;

    public string Name => "Frankfurter";

    public FrankfurterProvider(
        FrankfurterApiClient frankfurterApiClient,
        IOptions<ExchangeRateOptions> options,
        ILogger<FrankfurterProvider> logger)
    {
        _frankfurterApiClient = frankfurterApiClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<ExchangeRateDto?> GetExchangeRateAsync(
        string fromCurrency,
        string toCurrency,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var model = await _frankfurterApiClient.GetLatestRatesAsync(fromCurrency, toCurrency, cancellationToken);
            
            if (model?.Rates == null || !model.Rates.ContainsKey(toCurrency))
            {
                _logger.LogWarning("Frankfurter API response missing rate data for {From} to {To}", 
                    fromCurrency, toCurrency);
                return null;
            }

            var rate = model.Rates[toCurrency];
            var fetchedAt = ParseDate(model.Date);

            return CreateExchangeRateDto(fromCurrency, toCurrency, rate, fetchedAt);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching exchange rate from Frankfurter: {From} to {To}", 
                fromCurrency, toCurrency);
            return null;
        }
    }

    public async Task<Dictionary<string, ExchangeRateDto>?> GetMultipleRatesAsync(
        string baseCurrency,
        IEnumerable<string> targetCurrencies,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var model = await _frankfurterApiClient.GetLatestRatesAsync(baseCurrency, targetCurrencies, cancellationToken);
            
            if (model?.Rates == null)
            {
                _logger.LogWarning("Frankfurter API response missing rates data for bulk request from {Base}", baseCurrency);
                return null;
            }

            var fetchedAt = ParseDate(model.Date);
            var result = new Dictionary<string, ExchangeRateDto>();

            foreach (var kvp in model.Rates)
            {
                result[kvp.Key] = CreateExchangeRateDto(baseCurrency, kvp.Key, kvp.Value, fetchedAt);
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching bulk exchange rates from Frankfurter for {Base}", baseCurrency);
            return null;
        }
    }

    private ExchangeRateDto CreateExchangeRateDto(
        string fromCurrency,
        string toCurrency,
        decimal rate,
        DateTime fetchedAt)
    {
        return new ExchangeRateDto
        {
            FromCurrency = fromCurrency.ToUpperInvariant(),
            ToCurrency = toCurrency.ToUpperInvariant(),
            Rate = rate,
            FetchedAt = fetchedAt,
            Source = Name
        };
    }

    private DateTime ParseDate(string? dateStr)
    {
        return DateTime.Parse(dateStr ?? DateTime.UtcNow.ToString("yyyy-MM-dd"));
    }
}