namespace Maliev.CurrencyService.Api.Services
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Maliev.CurrencyService.Api.Models;
    using Maliev.CurrencyService.Data.Model;

    /// <summary>
    /// Defines the interface for the currency service.
    /// </summary>
    public interface ICurrencyService
    {
        /// <summary>
        /// Gets all currencies.
        /// </summary>
        /// <returns>A list of currency DTOs.</returns>
        Task<IEnumerable<CurrencyDto>> GetAllCurrenciesAsync();

        /// <summary>
        /// Gets a currency by its ID.
        /// </summary>
        /// <param name="id">The ID of the currency.</param>
        /// <returns>The currency DTO, or null if not found.</returns>
        Task<CurrencyDto?> GetCurrencyByIdAsync(int id);

        /// <summary>
        /// Creates a new currency.
        /// </summary>
        /// <param name="request">The request DTO for creating a currency.</param>
        /// <returns>The created currency DTO.</returns>
        Task<CurrencyDto> CreateCurrencyAsync(CreateCurrencyRequest request);

        /// <summary>
        /// Updates an existing currency.
        /// </summary>
        /// <param name="id">The ID of the currency to update.</param>
        /// <param name="request">The request DTO for updating a currency.</param>
        /// <returns>True if the currency was updated, false otherwise.</returns>
        Task<bool> UpdateCurrencyAsync(int id, UpdateCurrencyRequest request);

        /// <summary>
        /// Deletes a currency by its ID.
        /// </summary>
        /// <param name="id">The ID of the currency to delete.</param>
        /// <returns>True if the currency was deleted, false otherwise.</returns>
        Task<bool> DeleteCurrencyAsync(int id);

        /// <summary>
        /// Gets live exchange rates.
        /// </summary>
        /// <param name="baseCurrency">The base currency.</param>
        /// <param name="targetCurrency">The target currency.</param>
        /// <returns>The OpenRatesModel containing exchange rates.</returns>
        Task<OpenRatesModel> GetLiveExchangeRatesAsync(string baseCurrency, string targetCurrency);
    }
}