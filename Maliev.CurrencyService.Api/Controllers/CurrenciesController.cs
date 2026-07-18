using Asp.Versioning;
using Maliev.CurrencyService.Api.Models;
using Maliev.CurrencyService.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Maliev.CurrencyService.Api.Controllers;

[ApiController]
[Route("currencies/v{version:apiVersion}")]
[ApiVersion("1.0")]
[EnableRateLimiting("CurrencyPolicy")]
[Authorize] // Require valid JWT token for all endpoints
public class CurrenciesController : ControllerBase
{
    private readonly ICurrencyService _currencyService;
    private readonly ILogger<CurrenciesController> _logger;

    public CurrenciesController(ICurrencyService currencyService, ILogger<CurrenciesController> logger)
    {
        _currencyService = currencyService;
        _logger = logger;
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(CurrencyDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<CurrencyDto>> GetById(int id)
    {
        _logger.LogDebug("Getting currency by ID: {Id}", id);

        var currency = await _currencyService.GetByIdAsync(id);

        if (currency == null)
        {
            _logger.LogWarning("Currency not found with ID: {Id}", id);
            return NotFound($"Currency with ID {id} not found");
        }

        return Ok(currency);
    }

    [HttpGet("code/{code}")]
    [ProducesResponseType(typeof(CurrencyDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<CurrencyDto>> GetByCode(string code)
    {
        _logger.LogDebug("Getting currency by code: {Code}", code);

        if (string.IsNullOrWhiteSpace(code) || code.Length != 3)
        {
            return BadRequest("Currency code must be exactly 3 characters");
        }

        var currency = await _currencyService.GetByShortNameAsync(code);

        if (currency == null)
        {
            _logger.LogWarning("Currency not found with code: {Code}", code);
            return NotFound($"Currency with code {code.ToUpperInvariant()} not found");
        }

        return Ok(currency);
    }

    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<CurrencyDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<PagedResult<CurrencyDto>>> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null)
    {
        _logger.LogDebug("Getting currencies - Page: {Page}, PageSize: {PageSize}, Search: {Search}",
            page, pageSize, search);

        if (page < 1)
        {
            return BadRequest("Page must be greater than 0");
        }

        if (pageSize < 1 || pageSize > 100)
        {
            return BadRequest("PageSize must be between 1 and 100");
        }

        var result = await _currencyService.GetAllAsync(page, pageSize, search);

        _logger.LogDebug("Retrieved {Count} currencies out of {Total}",
            result.Items.Count(), result.TotalCount);

        return Ok(result);
    }

    [HttpGet("codes")]
    [ProducesResponseType(typeof(IEnumerable<string>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<IEnumerable<string>>> GetCurrencyCodes()
    {
        _logger.LogDebug("Getting all currency codes");

        var codes = await _currencyService.GetCurrencyCodesAsync();

        return Ok(codes);
    }

    [HttpPost]
    [ProducesResponseType(typeof(CurrencyDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<CurrencyDto>> Create([FromBody] CreateCurrencyRequest request)
    {
        _logger.LogDebug("Creating currency: {ShortName} - {LongName}",
            request.ShortName, request.LongName);

        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        try
        {
            var currency = await _currencyService.CreateAsync(request);

            _logger.LogInformation("Created currency: {ShortName} - {LongName} with ID {Id}",
                currency.ShortName, currency.LongName, currency.Id);

            return CreatedAtAction(nameof(GetById), new { id = currency.Id }, currency);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("duplicate") || ex.Message.Contains("unique"))
        {
            _logger.LogWarning("Attempt to create duplicate currency: {ShortName} - {LongName}",
                request.ShortName, request.LongName);
            return Conflict("A currency with this code or name already exists");
        }
        catch (DbUpdateException ex) when (
            ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            _logger.LogWarning("Attempt to create duplicate currency: {ShortName} - {LongName}",
                request.ShortName, request.LongName);
            return Conflict("A currency with this code or name already exists");
        }
    }

    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(CurrencyDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<CurrencyDto>> Update(int id, [FromBody] UpdateCurrencyRequest request)
    {
        _logger.LogDebug("Updating currency ID {Id}: {ShortName} - {LongName}",
            id, request.ShortName, request.LongName);

        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        try
        {
            var currency = await _currencyService.UpdateAsync(id, request);

            if (currency == null)
            {
                _logger.LogWarning("Attempt to update non-existent currency ID: {Id}", id);
                return NotFound($"Currency with ID {id} not found");
            }

            _logger.LogInformation("Updated currency ID {Id}: {ShortName} - {LongName}",
                id, currency.ShortName, currency.LongName);

            return Ok(currency);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("duplicate") || ex.Message.Contains("unique"))
        {
            _logger.LogWarning("Attempt to update currency ID {Id} with duplicate data: {ShortName} - {LongName}",
                id, request.ShortName, request.LongName);
            return Conflict("A currency with this code or name already exists");
        }
        catch (DbUpdateException ex) when (
            ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            _logger.LogWarning("Attempt to update currency ID {Id} with duplicate data: {ShortName} - {LongName}",
                id, request.ShortName, request.LongName);
            return Conflict("A currency with this code or name already exists");
        }
    }

    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> Delete(int id)
    {
        _logger.LogDebug("Deleting currency ID: {Id}", id);

        var deleted = await _currencyService.DeleteAsync(id);

        if (!deleted)
        {
            _logger.LogWarning("Attempt to delete non-existent currency ID: {Id}", id);
            return NotFound($"Currency with ID {id} not found");
        }

        _logger.LogInformation("Deleted currency ID: {Id}", id);

        return NoContent();
    }
}
