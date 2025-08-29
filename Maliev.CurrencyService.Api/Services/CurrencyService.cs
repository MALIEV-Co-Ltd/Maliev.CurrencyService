#nullable enable

namespace Maliev.CurrencyService.Api.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net.Http;
    using System.Text.Json;
    using System.Threading.Tasks;
    using Maliev.CurrencyService.Api.Models;
    using Maliev.CurrencyService.Data.Data;
    using Maliev.CurrencyService.Data.Models;
    using Maliev.CurrencyService.Data.Model;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// Implements the currency service.
    /// </summary>
    public class CurrencyService : ICurrencyService
    {
        private readonly CurrencyContext _context;
        private readonly ILogger<CurrencyService> _logger;
        private readonly IHttpClientFactory _httpClientFactory;

        /// <summary>
        /// Initializes a new instance of the <see cref="CurrencyService"/> class.
        /// </summary>
        /// <param name="context">The database context.</param>
        /// <param name="logger">The logger.</param>
        /// <param name="httpClientFactory">The HTTP client factory.</param>
        public CurrencyService(CurrencyContext context, ILogger<CurrencyService> logger, IHttpClientFactory httpClientFactory)
        {
            _context = context;
            _logger = logger;
            _httpClientFactory = httpClientFactory;
        }

        /// <inheritdoc/>
        public async Task<IEnumerable<CurrencyDto>> GetAllCurrenciesAsync()
        {
            _logger.LogInformation("Getting all currencies.");
            var currencies = await _context.Currency.ToListAsync();
            return currencies.Select(c => new CurrencyDto
            {
                Id = c.Id,
                ShortName = c.ShortName,
                LongName = c.LongName,
                CreatedDate = c.CreatedDate,
                ModifiedDate = c.ModifiedDate
            });
        }

        /// <inheritdoc/>
        public async Task<CurrencyDto?> GetCurrencyByIdAsync(int id)
        {
            _logger.LogInformation("Getting currency with ID: {Id}", id);
            var currency = await _context.Currency.FindAsync(id);
            if (currency == null)
            {
                _logger.LogWarning("Currency with ID: {Id} not found.", id);
                return null;
            }
            return new CurrencyDto
            {
                Id = currency.Id,
                ShortName = currency.ShortName,
                LongName = currency.LongName,
                CreatedDate = currency.CreatedDate,
                ModifiedDate = currency.ModifiedDate
            };
        }

        /// <inheritdoc/>
        public async Task<CurrencyDto> CreateCurrencyAsync(CreateCurrencyRequest request)
        {
            _logger.LogInformation("Creating new currency: {ShortName} - {LongName}", request.ShortName, request.LongName);
            var currency = new Currency
            {
                ShortName = request.ShortName,
                LongName = request.LongName,
                CreatedDate = DateTime.UtcNow,
                ModifiedDate = DateTime.UtcNow
            };
            _context.Currency.Add(currency);
            await _context.SaveChangesAsync();
            _logger.LogInformation("Currency created with ID: {Id}", currency.Id);
            return new CurrencyDto
            {
                Id = currency.Id,
                ShortName = currency.ShortName,
                LongName = currency.LongName,
                CreatedDate = currency.CreatedDate,
                ModifiedDate = currency.ModifiedDate
            };
        }

        /// <inheritdoc/>
        public async Task<bool> UpdateCurrencyAsync(int id, UpdateCurrencyRequest request)
        {
            _logger.LogInformation("Updating currency with ID: {Id}", id);
            var currency = await _context.Currency.FindAsync(id);
            if (currency == null)
            {
                _logger.LogWarning("Currency with ID: {Id} not found for update.", id);
                return false;
            }

            currency.ShortName = request.ShortName;
            currency.LongName = request.LongName;
            currency.ModifiedDate = DateTime.UtcNow;

            _context.Entry(currency).State = EntityState.Modified;
            try
            {
                await _context.SaveChangesAsync();
                _logger.LogInformation("Currency with ID: {Id} updated successfully.", id);
                return true;
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!CurrencyExists(id))
                {
                    _logger.LogError("Concurrency issue: Currency with ID: {Id} no longer exists.", id);
                    return false;
                }
                else
                {
                    throw;
                }
            }
        }

        /// <inheritdoc/>
        public async Task<bool> DeleteCurrencyAsync(int id)
        {
            _logger.LogInformation("Deleting currency with ID: {Id}", id);
            var currency = await _context.Currency.FindAsync(id);
            if (currency == null)
            {
                _logger.LogWarning("Currency with ID: {Id} not found for deletion.", id);
                return false;
            }

            _context.Currency.Remove(currency);
            await _context.SaveChangesAsync();
            _logger.LogInformation("Currency with ID: {Id} deleted successfully.", id);
            return true;
        }

        /// <inheritdoc/>
        public async Task<OpenRatesModel> GetLiveExchangeRatesAsync(string baseCurrency, string targetCurrency)
        {
            _logger.LogInformation("Getting live exchange rates for {BaseCurrency} to {TargetCurrency}", baseCurrency, targetCurrency);
            var client = _httpClientFactory.CreateClient();
            var response = await client.GetAsync($"https://api.frankfurter.app/latest?from={baseCurrency}&to={targetCurrency}");
            response.EnsureSuccessStatusCode();
            var jsonString = await response.Content.ReadAsStringAsync();
            var openRatesModel = JsonSerializer.Deserialize<OpenRatesModel>(jsonString);
            _logger.LogInformation("Successfully retrieved live exchange rates.");
            return openRatesModel ?? new OpenRatesModel { Base = baseCurrency, Date = DateTime.Today, Rates = new Dictionary<string, string>() };
        }

        private bool CurrencyExists(int id)
        {
            return _context.Currency.Any(e => e.Id == id);
        }
    }
}