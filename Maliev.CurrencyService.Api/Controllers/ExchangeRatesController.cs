namespace Maliev.CurrencyService.Api.Controllers
{
    using System.Threading.Tasks;
    using Maliev.CurrencyService.Api.Services;
    using Maliev.CurrencyService.Data.Model;
    using Microsoft.AspNetCore.Authentication.JwtBearer;
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// Controller for managing exchange rates.
    /// </summary>
    [Route("currencies/exchangerates")]
    [ApiController]
    [ApiConventionType(typeof(DefaultApiConventions))]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public class ExchangeRatesController : ControllerBase
    {
        private readonly ICurrencyService _currencyService;
        private readonly ILogger<ExchangeRatesController> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="ExchangeRatesController"/> class.
        /// </summary>
        /// <param name="currencyService">The currency service.</param>
        /// <param name="logger">The logger.</param>
        public ExchangeRatesController(ICurrencyService currencyService, ILogger<ExchangeRatesController> logger)
        {
            _currencyService = currencyService;
            _logger = logger;
        }

        /// <summary>
        /// Get live exchange rate.
        /// </summary>
        /// <param name="baseCurrency">The base currency.</param>
        /// <param name="targetCurrency">The target currency.</param>
        /// <returns>
        ///   <see cref="ActionResult{OpenRatesModel}" />.
        /// </returns>
        [HttpGet]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<OpenRatesModel>> GetLiveExchangeRatesAsync(string baseCurrency, string targetCurrency)
        {
            if (string.IsNullOrEmpty(baseCurrency) || string.IsNullOrEmpty(targetCurrency))
            {
                _logger.LogWarning("Base currency or target currency is null or empty.");
                return BadRequest("Base currency and target currency cannot be empty.");
            }

            _logger.LogInformation("Received request for live exchange rates from {BaseCurrency} to {TargetCurrency}", baseCurrency, targetCurrency);
            var result = await _currencyService.GetLiveExchangeRatesAsync(baseCurrency, targetCurrency);
            return Ok(result);
        }
    }
}