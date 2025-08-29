namespace Maliev.CurrencyService.Api.Controllers
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Maliev.CurrencyService.Api.Models;
    using Maliev.CurrencyService.Api.Services;
    using Microsoft.AspNetCore.Authentication.JwtBearer;
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// Controller for managing currencies.
    /// </summary>
    [Route("currencies")]
    [ApiController]
    [ApiConventionType(typeof(DefaultApiConventions))]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public class CurrenciesController : ControllerBase
    {
        private readonly ICurrencyService _currencyService;
        private readonly ILogger<CurrenciesController> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="CurrenciesController"/> class.
        /// </summary>
        /// <param name="currencyService">The currency service.</param>
        /// <param name="logger">The logger.</param>
        public CurrenciesController(ICurrencyService currencyService, ILogger<CurrenciesController> logger)
        {
            _currencyService = currencyService;
            _logger = logger;
        }

        /// <summary>
        /// Creates a new currency.
        /// </summary>
        /// <param name="request">The request DTO for creating a currency.</param>
        /// <returns>The created currency DTO.</returns>
        [HttpPost]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<CurrencyDto>> CreateCurrencyAsync([FromBody] CreateCurrencyRequest request)
        {
            if (!ModelState.IsValid)
            {
                _logger.LogWarning("Invalid model state for CreateCurrencyAsync.");
                return BadRequest(ModelState);
            }

            _logger.LogInformation("Received request to create currency: {ShortName}", request.ShortName);
            var createdCurrency = await _currencyService.CreateCurrencyAsync(request);
            return CreatedAtRoute("GetCurrency", new { id = createdCurrency.Id }, createdCurrency);
        }

        /// <summary>
        /// Deletes a currency by its ID.
        /// </summary>
        /// <param name="id">The ID of the currency to delete.</param>
        /// <returns>No content if successful, or not found.</n>
        [HttpDelete("{id:int}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult> DeleteCurrencyAsync(int id)
        {
            _logger.LogInformation("Received request to delete currency with ID: {Id}", id);
            var deleted = await _currencyService.DeleteCurrencyAsync(id);
            if (!deleted)
            {
                _logger.LogWarning("Currency with ID: {Id} not found for deletion.", id);
                return NotFound();
            }
            return NoContent();
        }

        /// <summary>
        /// Gets all currencies.
        /// </summary>
        /// <returns>A list of currency DTOs.</returns>
        [HttpGet]
        [AllowAnonymous]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<IEnumerable<CurrencyDto>>> GetAllCurrenciesAsync()
        {
            _logger.LogInformation("Received request to get all currencies.");
            var currencies = await _currencyService.GetAllCurrenciesAsync();
            if (currencies == null || !currencies.Any())
            {
                _logger.LogInformation("No currencies found.");
                return NotFound();
            }
            return Ok(currencies);
        }

        /// <summary>
        /// Gets a currency by its ID.
        /// </summary>
        /// <param name="id">The ID of the currency.</param>
        /// <returns>The currency DTO, or not found.</returns>
        [HttpGet("{id:int}", Name = "GetCurrency")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<CurrencyDto>> GetCurrencyAsync(int id)
        {
            _logger.LogInformation("Received request to get currency with ID: {Id}", id);
            var currency = await _currencyService.GetCurrencyByIdAsync(id);
            if (currency == null)
            {
                _logger.LogWarning("Currency with ID: {Id} not found.", id);
                return NotFound();
            }
            return Ok(currency);
        }

        /// <summary>
        /// Updates an existing currency.
        /// </summary>
        /// <param name="id">The ID of the currency to update.</param>
        /// <param name="request">The request DTO for updating a currency.</param>
        /// <returns>No content if successful, or bad request/not found.</returns>
        [HttpPut("{id:int}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult> UpdateCurrencyAsync(int id, [FromBody] UpdateCurrencyRequest request)
        {
            if (!ModelState.IsValid)
            {
                _logger.LogWarning("Invalid model state for UpdateCurrencyAsync for ID: {Id}.", id);
                return BadRequest(ModelState);
            }

            _logger.LogInformation("Received request to update currency with ID: {Id}", id);
            var updated = await _currencyService.UpdateCurrencyAsync(id, request);
            if (!updated)
            {
                _logger.LogWarning("Currency with ID: {Id} not found for update.", id);
                return NotFound();
            }
            return NoContent();
        }
    }
}