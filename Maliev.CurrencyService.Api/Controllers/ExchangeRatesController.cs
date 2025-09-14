using Asp.Versioning;
using Maliev.CurrencyService.Api.Models;
using Maliev.CurrencyService.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Maliev.CurrencyService.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("currencies/v{version:apiVersion}/exchange-rates")]
[Authorize]
[EnableRateLimiting("CurrencyPolicy")]
public class ExchangeRatesController : ControllerBase
{
    private readonly IExchangeRateService _exchangeRateService;
    private readonly ILogger<ExchangeRatesController> _logger;

    public ExchangeRatesController(IExchangeRateService exchangeRateService, ILogger<ExchangeRatesController> logger)
    {
        _exchangeRateService = exchangeRateService;
        _logger = logger;
    }

    /// <summary>
    /// Get exchange rate between two currencies
    /// </summary>
    /// <param name="from">Source currency code (e.g., THB)</param>
    /// <param name="to">Target currency code (e.g., USD)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Exchange rate information</returns>
    [HttpGet]
    [ProducesResponseType(typeof(ExchangeRateDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<ExchangeRateDto>> GetExchangeRate(
        [FromQuery] string from, 
        [FromQuery] string to,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(from) || string.IsNullOrWhiteSpace(to))
        {
            return BadRequest("Both 'from' and 'to' currency codes are required");
        }

        if (from.Length != 3 || to.Length != 3)
        {
            return BadRequest("Currency codes must be exactly 3 characters long");
        }

        try
        {
            var rate = await _exchangeRateService.GetExchangeRateAsync(from, to, cancellationToken);
            
            if (rate == null)
            {
                return NotFound($"Exchange rate not available for {from} to {to}");
            }

            return Ok(rate);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting exchange rate for {From} to {To}", from, to);
            return StatusCode(500, "An error occurred while fetching exchange rate");
        }
    }

    /// <summary>
    /// Get multiple exchange rates from a base currency to multiple target currencies
    /// </summary>
    /// <param name="from">Base currency code (e.g., THB)</param>
    /// <param name="to">Comma-separated target currency codes (e.g., USD,EUR,JPY)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Dictionary of exchange rates</returns>
    [HttpGet("bulk")]
    [ProducesResponseType(typeof(Dictionary<string, ExchangeRateDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<Dictionary<string, ExchangeRateDto>>> GetMultipleExchangeRates(
        [FromQuery] string from,
        [FromQuery] string to,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(from) || string.IsNullOrWhiteSpace(to))
        {
            return BadRequest("Both 'from' and 'to' parameters are required");
        }

        if (from.Length != 3)
        {
            return BadRequest("Base currency code must be exactly 3 characters long");
        }

        var targetCurrencies = to.Split(',', StringSplitOptions.RemoveEmptyEntries)
                                .Select(c => c.Trim().ToUpperInvariant())
                                .Where(c => c.Length == 3)
                                .Distinct()
                                .ToList();

        if (!targetCurrencies.Any())
        {
            return BadRequest("At least one valid target currency code is required");
        }

        if (targetCurrencies.Count > 20)
        {
            return BadRequest("Maximum 20 target currencies allowed per request");
        }

        try
        {
            var rates = await _exchangeRateService.GetMultipleRatesAsync(from, targetCurrencies, cancellationToken);
            return Ok(rates);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting multiple exchange rates for {From} to [{To}]", from, to);
            return StatusCode(500, "An error occurred while fetching exchange rates");
        }
    }

    /// <summary>
    /// Convert an amount from one currency to another
    /// </summary>
    /// <param name="request">Currency conversion request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Conversion result with exchange rate information</returns>
    [HttpPost("convert")]
    [ProducesResponseType(typeof(ConvertCurrencyResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<ConvertCurrencyResponse>> ConvertCurrency(
        [FromBody] ConvertCurrencyRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        try
        {
            var result = await _exchangeRateService.ConvertCurrencyAsync(request, cancellationToken);
            
            if (result == null)
            {
                return NotFound($"Exchange rate not available for {request.From} to {request.To}");
            }

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error converting {Amount} from {From} to {To}", 
                request.Amount, request.From, request.To);
            return StatusCode(500, "An error occurred while converting currency");
        }
    }
}