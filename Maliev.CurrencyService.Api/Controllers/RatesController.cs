using Asp.Versioning;
using FluentValidation;
using Maliev.CurrencyService.Api.Models.Common;
using Maliev.CurrencyService.Api.Models.Rates;
using Maliev.CurrencyService.Api.Services;
using Microsoft.AspNetCore.Mvc;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Maliev.CurrencyService.Api.Controllers;

/// <summary>
/// Exchange rate endpoints for live and snapshot queries
/// </summary>
/// <remarks>
/// User Story 2: Provides live exchange rate retrieval with caching and provider fallback.
/// </remarks>
[ApiController]
[ApiVersion("1.0")]
[Route("currencies/v{version:apiVersion}/rates")]
[Produces("application/json")]
public class RatesController : ControllerBase
{
    private readonly IRateService _rateService;
    private readonly IValidator<RateQueryRequest> _validator;
    private readonly ILogger<RatesController> _logger;

    public RatesController(
        IRateService rateService,
        IValidator<RateQueryRequest> validator,
        ILogger<RatesController> logger)
    {
        _rateService = rateService;
        _validator = validator;
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
            // Create and validate request
            var request = new RateQueryRequest
            {
                From = from?.ToUpperInvariant() ?? string.Empty,
                To = to?.ToUpperInvariant() ?? string.Empty,
                Mode = mode?.ToLower() ?? "live",
                Date = date
            };

            var validationResult = await _validator.ValidateAsync(request, cancellationToken);
            if (!validationResult.IsValid)
            {
                _logger.LogWarning("Validation failed for rate query: {Errors}",
                    string.Join(", ", validationResult.Errors.Select(e => e.ErrorMessage)));

                return BadRequest(new ErrorResponse
                {
                    Error = "BadRequest",
                    Message = "Invalid request parameters",
                    Timestamp = DateTime.UtcNow,
                    CorrelationId = HttpContext.TraceIdentifier,
                    Details = validationResult.Errors
                        .GroupBy(e => e.PropertyName)
                        .ToDictionary(
                            g => g.Key,
                            g => g.Select(e => e.ErrorMessage).ToArray())
                });
            }

            _logger.LogInformation("GET /v1/rates - From: {From}, To: {To}, Mode: {Mode}, Date: {Date}",
                request.From, request.To, request.Mode, request.Date);

            // Get rate based on mode (User Story 2: live, User Story 3: snapshot)
            ExchangeRateResponse? rateResponse;
            if (request.Mode == "snapshot")
            {
                if (!request.Date.HasValue)
                {
                    return BadRequest(new ErrorResponse
                    {
                        Error = "BadRequest",
                        Message = "Date parameter is required for snapshot mode",
                        Timestamp = DateTime.UtcNow,
                        CorrelationId = HttpContext.TraceIdentifier
                    });
                }

                rateResponse = await _rateService.GetSnapshotRateAsync(
                    request.From,
                    request.To,
                    request.Date.Value,
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
            var etag = GenerateETag(rateResponse);

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
    /// Generates ETag for response caching
    /// </summary>
    private static string GenerateETag(object content)
    {
        var json = JsonSerializer.Serialize(content);
        var bytes = Encoding.UTF8.GetBytes(json);
        var hash = SHA256.HashData(bytes);
        return Convert.ToBase64String(hash)[..16]; // First 16 chars of base64 hash
    }
}
