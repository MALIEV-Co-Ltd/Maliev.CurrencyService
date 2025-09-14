using Maliev.CurrencyService.Api.Models;
using Maliev.CurrencyService.Api.Models.ApiResponses;
using Maliev.CurrencyService.Api.Services.Clients;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace Maliev.CurrencyService.Api.Services;

public class FawazahmedProvider : IExchangeRateProvider
{
    private readonly FawazahmedApiClient _fawazahmedApiClient;
    private readonly ExchangeRateOptions _options;
    private readonly ILogger<FawazahmedProvider> _logger;

    public string Name => "Fawazahmed";

    public FawazahmedProvider(
        FawazahmedApiClient fawazahmedApiClient,
        IOptions<ExchangeRateOptions> options,
        ILogger<FawazahmedProvider> logger)
    {
        _fawazahmedApiClient = fawazahmedApiClient;
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
            var fromLower = fromCurrency.ToLowerInvariant();
            var toLower = toCurrency.ToLowerInvariant();
            
            var model = await _fawazahmedApiClient.GetCurrencyRatesAsync(fromLower, cancellationToken);
            
            if (model == null ||
                !model.TryGetValue("date", out var dateObj) ||
                !model.TryGetValue(fromLower, out var currencyRatesObj) ||
                !(currencyRatesObj is JsonElement currencyRatesElement) ||
                !currencyRatesElement.TryGetProperty(toLower, out var rateElement))
            {
                _logger.LogWarning("Fawazahmed API response missing rate data for {From} to {To}", 
                    fromCurrency, toCurrency);
                return null;
            }

            var rate = rateElement.GetDecimal();
            var fetchedAt = ParseDate(dateObj?.ToString());

            return CreateExchangeRateDto(fromCurrency, toCurrency, rate, fetchedAt);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching exchange rate from Fawazahmed: {From} to {To}", 
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
            var baseLower = baseCurrency.ToLowerInvariant();
            var model = await _fawazahmedApiClient.GetCurrencyRatesAsync(baseLower, cancellationToken);
            
            if (model == null ||
                !model.TryGetValue("date", out var dateObj) ||
                !model.TryGetValue(baseLower, out var ratesObj) ||
                !(ratesObj is JsonElement ratesElement))
            {
                _logger.LogWarning("Fawazahmed API response missing rates data for bulk request from {Base}", baseCurrency);
                return null;
            }

            var fetchedAt = ParseDate(dateObj?.ToString());
            var result = new Dictionary<string, ExchangeRateDto>();

            foreach (var target in targetCurrencies)
            {
                var targetLower = target.ToLowerInvariant();
                if (ratesElement.TryGetProperty(targetLower, out var rateElement))
                {
                    result[target.ToUpperInvariant()] = CreateExchangeRateDto(baseCurrency, target, rateElement.GetDecimal(), fetchedAt);
                }
            }

            return result.Any() ? result : null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching bulk exchange rates from Fawazahmed for {Base}", baseCurrency);
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