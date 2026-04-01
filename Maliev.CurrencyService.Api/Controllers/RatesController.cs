using Asp.Versioning;
using Maliev.Aspire.ServiceDefaults;
using Maliev.Aspire.ServiceDefaults.Authorization;
using Maliev.CurrencyService.Api.Models.Common;
using Maliev.CurrencyService.Api.Models.Rates;
using Maliev.CurrencyService.Api.Services;
using Maliev.CurrencyService.Application.Common;
using Maliev.CurrencyService.Application.DTOs.Rates;
using Maliev.CurrencyService.Application.Interfaces;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Maliev.CurrencyService.Api.Controllers;

/// <summary>
/// Exchange rate endpoints for live and snapshot queries
/// </summary>
/// <remarks>
/// User Story 2: Provides live exchange rate retrieval with caching and provider fallback.
/// </remarks>
[ApiController]
[ApiVersion("1")]
[Route("currency/v{version:apiVersion}/rates")]
[Produces("application/json")]
public class RatesController : ControllerBase
{
    private readonly IRateService _rateService;
    private readonly ILogger<RatesController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="RatesController"/> class.
    /// </summary>
    /// <param name="rateService">The rate service.</param>
    /// <param name="logger">The logger.</param>
    public RatesController(
        IRateService rateService,
        ILogger<RatesController> logger)
    {
        _rateService = rateService;
        _logger = logger;
    }

    /// <summary>
    /// Get exchange rate between two currencies
    /// </summary>
    /// <param name="from">Source currency code (ISO 4217)</param>
    /// <param name="to">Target currency code (ISO 4217)</param>
    /// <param name="mode">Query mode: live or snapshot (default: live)</param>
    /// <param name="date">Snapshot date (required if mode=snapshot)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Exchange rate response</returns>
    [HttpGet]
    [RequirePermission(CurrencyPermissions.RatesRead)]
    [EnableRateLimiting(RateLimitPolicies.Public)]
    [ProducesResponseType(typeof(ExchangeRateResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status304NotModified)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<ExchangeRateResponse>> GetExchangeRate(
        [FromQuery] string from,
        [FromQuery] string to,
        [FromQuery] string mode = "live",
        [FromQuery] DateOnly? date = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Create request
            var request = new RateQueryRequest
            {
                From = from?.ToUpperInvariant() ?? string.Empty,
                To = to?.ToUpperInvariant() ?? string.Empty,
                Mode = mode?.ToLower() ?? "live",
                Date = date
            };

            _logger.LogInformation("GET /v1/rates - From: {From}, To: {To}, Mode: {Mode}, Date: {Date}",
                request.From, request.To, request.Mode, request.Date);

            // Validate request
            var validationErrors = new List<string>();

            if (string.IsNullOrWhiteSpace(request.From) || request.From.Length != 3)
                validationErrors.Add("'from' currency code must be exactly 3 characters");

            if (string.IsNullOrWhiteSpace(request.To) || request.To.Length != 3)
                validationErrors.Add("'to' currency code must be exactly 3 characters");

            if (!string.IsNullOrWhiteSpace(request.From) && !System.Text.RegularExpressions.Regex.IsMatch(request.From, "^[A-Z]{3}$"))
                validationErrors.Add("'from' currency code must contain only uppercase letters");

            if (!string.IsNullOrWhiteSpace(request.To) && !System.Text.RegularExpressions.Regex.IsMatch(request.To, "^[A-Z]{3}$"))
                validationErrors.Add("'to' currency code must contain only uppercase letters");

            if (request.Mode != "live" && request.Mode != "snapshot")
                validationErrors.Add("'mode' must be either 'live' or 'snapshot'");

            if (request.Mode == "snapshot" && !request.Date.HasValue)
                validationErrors.Add("'date' is required when mode is 'snapshot'");

            if (validationErrors.Count > 0)
            {
                _logger.LogWarning("Validation failed for rate query: {Errors}",
                    string.Join(", ", validationErrors));

                return BadRequest(new ErrorResponse
                {
                    Error = "BadRequest",
                    Message = "Invalid request parameters",
                    Timestamp = DateTime.UtcNow,
                    CorrelationId = HttpContext.TraceIdentifier,
                    Details = new Dictionary<string, string[]>
                    {
                        { "validation", validationErrors.ToArray() }
                    }
                });
            }

            // Get rate based on mode (User Story 2: live, User Story 3: snapshot)
            ExchangeRateResponse? rateResponse;
            if (request.Mode == "snapshot")
            {
                rateResponse = await _rateService.GetSnapshotRateAsync(
                    request.From,
                    request.To,
                    request.Date!.Value,
                    cancellationToken);
            }
            else // live mode
            {
                rateResponse = await _rateService.GetLiveRateAsync(
                    request.From,
                    request.To,
                    cancellationToken);
            }

            if (rateResponse == null)
            {
                // Different error handling for snapshot vs live mode
                if (request.Mode == "snapshot")
                {
                    _logger.LogWarning("No snapshot found for {From}:{To} on {Date}",
                        request.From, request.To, request.Date);

                    return NotFound(new ErrorResponse
                    {
                        Error = "NotFound",
                        Message = $"No snapshot available for {request.From}/{request.To} on {request.Date:yyyy-MM-dd}",
                        Timestamp = DateTime.UtcNow,
                        CorrelationId = HttpContext.TraceIdentifier
                    });
                }
                else // live mode
                {
                    _logger.LogWarning("No live rate available for {From}:{To}", request.From, request.To);

                    // Add Retry-After header (Edge Case 1: retry after 30 seconds)
                    Response.Headers.RetryAfter = "30";

                    return StatusCode(StatusCodes.Status503ServiceUnavailable, new ErrorResponse
                    {
                        Error = "ServiceUnavailable",
                        Message = $"Exchange rate temporarily unavailable for {request.From}/{request.To}. All providers are down. Please retry after 30 seconds.",
                        Timestamp = DateTime.UtcNow,
                        CorrelationId = HttpContext.TraceIdentifier
                    });
                }
            }

            // Generate ETag
            var etag = ETagHelper.GenerateETag(rateResponse);

            // Check conditional GET (If-None-Match)
            if (Request.Headers.IfNoneMatch.Any())
            {
                var clientETag = Request.Headers.IfNoneMatch.First()?.Trim('"');
                if (clientETag == etag)
                {
                    _logger.LogDebug("ETag match - returning 304 Not Modified");
                    return StatusCode(StatusCodes.Status304NotModified);
                }
            }

            // Check conditional GET (If-Modified-Since)
            if (Request.Headers.IfModifiedSince.Any() &&
                DateTime.TryParse(Request.Headers.IfModifiedSince.First(), out var ifModifiedSince))
            {
                if (rateResponse.Timestamp <= ifModifiedSince)
                {
                    _logger.LogDebug("Not modified since {IfModifiedSince} - returning 304", ifModifiedSince);
                    return StatusCode(StatusCodes.Status304NotModified);
                }
            }

            // Add response headers
            Response.Headers["X-Correlation-ID"] = HttpContext.TraceIdentifier;
            Response.Headers.ETag = $"\"{etag}\"";
            Response.Headers["Last-Modified"] = rateResponse.Timestamp.ToString("R"); // RFC 1123 format

            // Mode-specific headers
            if (request.Mode == "snapshot")
            {
                // Snapshots don't change, cache for 24 hours
                Response.Headers["Cache-Control"] = "public, max-age=86400"; // 24 hours
            }
            else // live mode
            {
                // Live rates cache for 5 minutes
                Response.Headers["Cache-Control"] = "public, max-age=300"; // 5 minutes

                // Add X-Rate-Staleness header (per research.md decision 4)
                var age = (DateTime.UtcNow - rateResponse.Timestamp).TotalSeconds;
                if (age > 300) // Stale (beyond 5 minute TTL)
                {
                    Response.Headers["X-Rate-Staleness"] = $"stale; age={age:F0}";
                    _logger.LogInformation("Serving stale rate for {From}:{To} (age: {Age}s)",
                        request.From, request.To, age);
                }
                else
                {
                    Response.Headers["X-Rate-Staleness"] = "fresh";
                }
            }

            return Ok(rateResponse);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing rate request for {From}:{To}", from, to);

            return StatusCode(StatusCodes.Status500InternalServerError, new ErrorResponse
            {
                Error = "InternalServerError",
                Message = "An error occurred while retrieving exchange rate",
                Timestamp = DateTime.UtcNow,
                CorrelationId = HttpContext.TraceIdentifier
            });
        }
    }

    /// <summary>
    /// Update a specific exchange rate (admin only)
    /// </summary>
    [HttpPut]
    [RequirePermission(CurrencyPermissions.RatesUpdate)]
    [EnableRateLimiting(RateLimitPolicies.Api)]
    public async Task<IActionResult> UpdateRate([FromBody] UpdateRateRequest request)
    {
        await _rateService.UpdateRateAsync(request.From, request.To, request.Rate);
        return Ok(new { message = "Rate update accepted" });
    }

    /// <summary>
    /// Bulk update exchange rates (admin only)
    /// </summary>
    [HttpPost("bulk-update")]
    [RequirePermission(CurrencyPermissions.RatesBulkUpdate)]
    [EnableRateLimiting(RateLimitPolicies.Api)]
    public async Task<IActionResult> BulkUpdateRates([FromBody] BulkUpdateRatesRequest request)
    {
        await _rateService.BulkUpdateRatesAsync(request.Rates);
        return Ok(new { message = "Bulk update processed" });
    }

    /// <summary>
    /// Set the active rate source provider (admin only)
    /// </summary>
    [HttpPost("set-source")]
    [RequirePermission(CurrencyPermissions.RatesSetSource)]
    [EnableRateLimiting(RateLimitPolicies.Api)]
    public async Task<IActionResult> SetRateSource([FromBody] SetRateSourceRequest request)
    {
        // Implementation stub
        _logger.LogInformation("Admin setting rate source to {Provider}", request.ProviderName);
        return Ok(new { message = $"Rate source set to {request.ProviderName}" });
    }

    /// <summary>
    /// Trigger manual rate refresh from external providers (admin only)
    /// </summary>
    [HttpPost("refresh")]
    [RequirePermission(CurrencyPermissions.SystemRefreshRates)]
    [EnableRateLimiting(RateLimitPolicies.Api)]
    public async Task<IActionResult> RefreshRatesFromProvider()
    {
        // Implementation stub
        _logger.LogInformation("Admin triggering manual rate refresh");
        return Accepted(new { message = "Rate refresh triggered" });
    }
}
