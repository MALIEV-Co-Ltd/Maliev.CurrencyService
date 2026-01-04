using Asp.Versioning;
using Maliev.Aspire.ServiceDefaults.Authorization;
using Maliev.CurrencyService.Api.Models.Common;
using Maliev.CurrencyService.Api.Models.Currencies;
using Maliev.CurrencyService.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Maliev.CurrencyService.Api.Controllers;

/// <summary>
/// Currency metadata endpoints
/// </summary>
/// <remarks>
/// User Story 1: Provides currency metadata lookup and country-to-currency resolution.
/// </remarks>
[ApiController]
[ApiVersion("1.0")]
[Route("currency/v{version:apiVersion}/currencies")]
[Produces("application/json")]
public class CurrenciesController : ControllerBase
{
    private readonly ICurrencyService _currencyService;
    private readonly ILogger<CurrenciesController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="CurrenciesController"/> class.
    /// </summary>
    /// <param name="currencyService">The currency service.</param>
    /// <param name="logger">The logger.</param>
    public CurrenciesController(
        ICurrencyService currencyService,
        ILogger<CurrenciesController> logger)
    {
        _currencyService = currencyService;
        _logger = logger;
    }

    /// <summary>
    /// List all currencies with pagination
    /// </summary>
    /// <param name="page">Page number (1-indexed, default: 1)</param>
    /// <param name="pageSize">Number of items per page (1-200, default: 50)</param>
    /// <param name="isActive">Optional filter by active status</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Paginated currency list</returns>
    [HttpGet]
    [RequirePermission(CurrencyPermissions.CurrenciesRead)]
    [EnableRateLimiting("PublicApi")]
    [ProducesResponseType(typeof(PaginatedCurrencyResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<PaginatedCurrencyResponse>> ListCurrencies(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] bool? isActive = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("GET /v1/currencies - Page: {Page}, PageSize: {PageSize}, IsActive: {IsActive}",
                page, pageSize, isActive);

            var result = await _currencyService.GetAllAsync(page, pageSize, isActive, cancellationToken);

            // Add response headers
            Response.Headers["X-Correlation-ID"] = HttpContext.TraceIdentifier;
            Response.Headers["Cache-Control"] = "public, max-age=300"; // 5 minutes

            // Generate ETag based on result content
            var etag = GenerateETag(result);
            Response.Headers.ETag = $"\"{etag}\"";

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing currencies - Page: {Page}, PageSize: {PageSize}",
                page, pageSize);

            return StatusCode(StatusCodes.Status500InternalServerError, new ErrorResponse
            {
                Error = "InternalServerError",
                Message = "An error occurred while retrieving currencies",
                Timestamp = DateTime.UtcNow,
                CorrelationId = HttpContext.TraceIdentifier
            });
        }
    }

    /// <summary>
    /// Get a single currency by ID
    /// </summary>
    /// <param name="id">Currency unique identifier (GUID)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Currency details</returns>
    /// <response code="404">If the currency ID is not found.</response>
    [HttpGet("{id:guid}")]
    [RequirePermission(CurrencyPermissions.CurrenciesRead)]
    [ProducesResponseType(typeof(CurrencyResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<CurrencyResponse>> GetCurrencyById(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("GET /v1/currencies/{Id}", id);

            var currency = await _currencyService.GetByIdAsync(id, cancellationToken);

            if (currency == null)
            {
                _logger.LogWarning("Currency not found with ID: {Id}", id);

                return NotFound(new ErrorResponse
                {
                    Error = "NotFound",
                    Message = $"Currency not found with ID: {id}",
                    Timestamp = DateTime.UtcNow,
                    CorrelationId = HttpContext.TraceIdentifier
                });
            }

            // Add response headers
            Response.Headers["X-Correlation-ID"] = HttpContext.TraceIdentifier;
            Response.Headers["Cache-Control"] = "public, max-age=300"; // 5 minutes

            // Generate ETag based on currency content
            var etag = GenerateETag(currency);
            Response.Headers.ETag = $"\"{etag}\"";

            return Ok(currency);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving currency by ID: {Id}", id);

            return StatusCode(StatusCodes.Status500InternalServerError, new ErrorResponse
            {
                Error = "InternalServerError",
                Message = "An error occurred while retrieving the currency",
                Timestamp = DateTime.UtcNow,
                CorrelationId = HttpContext.TraceIdentifier
            });
        }
    }

    /// <summary>
    /// Resolve currency by country code (path parameter)
    /// </summary>
    /// <param name="countryCode">Country code in ISO2 (e.g., "TH") or ISO3 (e.g., "THA") format</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Primary currency for the country</returns>
    [HttpGet("~/currency/v{version:apiVersion}/countries/{countryCode}/currency")]
    [RequirePermission(CurrencyPermissions.CurrenciesRead)]
    [EnableRateLimiting("PublicApi")]
    [ProducesResponseType(typeof(CurrencyResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<CurrencyResponse>> GetCurrencyByCountryPath(
        string countryCode,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("GET /v1/countries/{CountryCode}/currency", countryCode);

            // Don't reveal format validation - treat all invalid codes as NotFound (FR-059)
            if (string.IsNullOrWhiteSpace(countryCode))
            {
                return NotFound(new ErrorResponse
                {
                    Error = "NotFound",
                    Message = $"No currency found for country code: {countryCode}",
                    Timestamp = DateTime.UtcNow,
                    CorrelationId = HttpContext.TraceIdentifier
                });
            }

            var currency = await _currencyService.GetByCountryCodeAsync(countryCode, cancellationToken);

            if (currency == null)
            {
                _logger.LogWarning("Currency not found for country code: {CountryCode}", countryCode);

                return NotFound(new ErrorResponse
                {
                    Error = "NotFound",
                    Message = $"No currency found for country code: {countryCode}",
                    Timestamp = DateTime.UtcNow,
                    CorrelationId = HttpContext.TraceIdentifier
                });
            }

            // Add response headers
            Response.Headers["X-Correlation-ID"] = HttpContext.TraceIdentifier;
            Response.Headers["Cache-Control"] = "public, max-age=3600"; // 1 hour (country mapping rarely changes)

            // Generate ETag based on currency content
            var etag = GenerateETag(currency);
            Response.Headers.ETag = $"\"{etag}\"";

            return Ok(currency);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resolving currency for country code: {CountryCode}", countryCode);

            return StatusCode(StatusCodes.Status500InternalServerError, new ErrorResponse
            {
                Error = "InternalServerError",
                Message = "An error occurred while resolving currency",
                Timestamp = DateTime.UtcNow,
                CorrelationId = HttpContext.TraceIdentifier
            });
        }
    }

    /// <summary>
    /// Resolve currency by country code (ISO2 or ISO3)
    /// </summary>
    /// <param name="iso">Country code in ISO2 (e.g., "TH") or ISO3 (e.g., "THA") format</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Primary currency for the country</returns>
    [HttpGet("by-country")]
    [RequirePermission(CurrencyPermissions.CurrenciesRead)]
    [EnableRateLimiting("PublicApi")]
    [ProducesResponseType(typeof(CurrencyResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<CurrencyResponse>> GetCurrencyByCountry(
        [FromQuery] string iso,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Validate country code format
            if (string.IsNullOrWhiteSpace(iso) || !System.Text.RegularExpressions.Regex.IsMatch(iso, @"^[A-Z]{2,3}$", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
            {
                _logger.LogWarning("Invalid country code format: {Iso}", iso);

                return BadRequest(new ErrorResponse
                {
                    Error = "BadRequest",
                    Message = "Country code must be in ISO2 (2 letters) or ISO3 (3 letters) format",
                    Timestamp = DateTime.UtcNow,
                    CorrelationId = HttpContext.TraceIdentifier
                });
            }

            _logger.LogInformation("GET /v1/currencies/by-country - ISO: {Iso}", iso);

            var currency = await _currencyService.GetByCountryCodeAsync(iso, cancellationToken);

            if (currency == null)
            {
                _logger.LogWarning("Currency not found for country code: {Iso}", iso);

                return NotFound(new ErrorResponse
                {
                    Error = "NotFound",
                    Message = $"No currency found for country code: {iso}",
                    Timestamp = DateTime.UtcNow,
                    CorrelationId = HttpContext.TraceIdentifier
                });
            }

            // Add response headers
            Response.Headers["X-Correlation-ID"] = HttpContext.TraceIdentifier;
            Response.Headers["Cache-Control"] = "public, max-age=3600"; // 1 hour (country mapping rarely changes)

            // Generate ETag based on currency content
            var etag = GenerateETag(currency);
            Response.Headers.ETag = $"\"{etag}\"";

            return Ok(currency);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resolving currency for country code: {Iso}", iso);

            return StatusCode(StatusCodes.Status500InternalServerError, new ErrorResponse
            {
                Error = "InternalServerError",
                Message = "An error occurred while resolving currency",
                Timestamp = DateTime.UtcNow,
                CorrelationId = HttpContext.TraceIdentifier
            });
        }
    }

    /// <summary>
    /// Get currency by code
    /// </summary>
    /// <param name="code">Currency code (ISO 4217)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Currency details</returns>
    [HttpGet("{code}")]
    [RequirePermission(CurrencyPermissions.CurrenciesRead)]
    [EnableRateLimiting("PublicApi")]
    [ProducesResponseType(typeof(CurrencyResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status304NotModified)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<CurrencyResponse>> GetByCode(
        string code,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("GET /v1/currencies/{Code}", code);

            var currency = await _currencyService.GetByCodeAsync(code, cancellationToken);

            if (currency == null)
            {
                return NotFound(new ErrorResponse
                {
                    Error = "NotFound",
                    Message = $"Currency '{code}' not found",
                    Timestamp = DateTime.UtcNow,
                    CorrelationId = HttpContext.TraceIdentifier
                });
            }

            // Generate ETag
            var etag = GenerateETag(currency);

            // Check If-None-Match
            if (Request.Headers.IfNoneMatch.Any())
            {
                var clientETag = Request.Headers.IfNoneMatch.First()?.Trim('"');
                if (clientETag == etag)
                {
                    return StatusCode(StatusCodes.Status304NotModified);
                }
            }

            Response.Headers.ETag = $"\"{etag}\"";
            Response.Headers["X-Correlation-ID"] = HttpContext.TraceIdentifier;
            Response.Headers["Cache-Control"] = "public, max-age=300"; // 5 minutes

            return Ok(currency);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving currency {Code}", code);

            return StatusCode(StatusCodes.Status500InternalServerError, new ErrorResponse
            {
                Error = "InternalServerError",
                Message = "An error occurred while retrieving currency",
                Timestamp = DateTime.UtcNow,
                CorrelationId = HttpContext.TraceIdentifier
            });
        }
    }

    /// <summary>
    /// Get currency by ID (admin only)
    /// </summary>
    /// <param name="id">Currency GUID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Currency details with ETag</returns>
    [HttpGet("~/currency/v{version:apiVersion}/admin/currencies/{id:guid}")]
    [Authorize] // Requires JWT authentication
    [EnableRateLimiting("AuthenticatedApi")]
    [ProducesResponseType(typeof(CurrencyResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<CurrencyResponse>> GetById(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("GET /v1/admin/currencies/{Id}", id);

            var currency = await _currencyService.GetByIdAsync(id, cancellationToken);

            if (currency == null)
            {
                return NotFound(new ErrorResponse
                {
                    Error = "NotFound",
                    Message = $"Currency with ID '{id}' not found",
                    Timestamp = DateTime.UtcNow,
                    CorrelationId = HttpContext.TraceIdentifier
                });
            }

            // Generate ETag for optimistic concurrency (FR-006)
            var etag = GenerateETag(currency);
            Response.Headers.ETag = $"\"{etag}\"";
            Response.Headers["X-Correlation-ID"] = HttpContext.TraceIdentifier;
            Response.Headers["Cache-Control"] = "private, max-age=0"; // Admin endpoint, no caching

            return Ok(currency);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving currency {Id}", id);

            return StatusCode(StatusCodes.Status500InternalServerError, new ErrorResponse
            {
                Error = "InternalServerError",
                Message = "An error occurred while retrieving currency",
                Timestamp = DateTime.UtcNow,
                CorrelationId = HttpContext.TraceIdentifier
            });
        }
    }

    /// <summary>
    /// Create a new currency (admin only)
    /// </summary>
    /// <param name="request">Currency creation request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Created currency</returns>
    [HttpPost("~/currency/v{version:apiVersion}/admin/currencies")]
    [Authorize] // Requires JWT authentication
    [RequirePermission(CurrencyPermissions.CurrenciesCreate)]
    [EnableRateLimiting("AuthenticatedApi")]
    [ProducesResponseType(typeof(CurrencyResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<CurrencyResponse>> CreateAdmin(
        [FromBody] CreateCurrencyRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("POST /v1/admin/currencies - Creating currency {Code}", request.Code);

            // Validate request
            var validationErrors = new List<string>();

            if (string.IsNullOrWhiteSpace(request.Code))
                validationErrors.Add("Code is required");
            else if (request.Code.Length != 3)
                validationErrors.Add("Code must be exactly 3 characters");
            else if (!System.Text.RegularExpressions.Regex.IsMatch(request.Code, "^[A-Z]{3}$"))
                validationErrors.Add("Code must be uppercase letters only");

            if (string.IsNullOrWhiteSpace(request.Symbol))
                validationErrors.Add("Symbol is required");

            if (string.IsNullOrWhiteSpace(request.Name))
                validationErrors.Add("Name is required");

            if (request.DecimalPlaces < 0)
                validationErrors.Add("DecimalPlaces must be non-negative");

            if (validationErrors.Count > 0)
            {
                return BadRequest(new ErrorResponse
                {
                    Error = "BadRequest",
                    Message = "Invalid currency creation request",
                    Timestamp = DateTime.UtcNow,
                    CorrelationId = HttpContext.TraceIdentifier,
                    Details = new Dictionary<string, string[]>
                    {
                        { "validation", validationErrors.ToArray() }
                    }
                });
            }

            var currency = await _currencyService.CreateAsync(request, cancellationToken);

            Response.Headers["X-Correlation-ID"] = HttpContext.TraceIdentifier;
            Response.Headers["Location"] = $"/v1/admin/currencies/{currency.Id}";

            return CreatedAtAction(nameof(GetById), new { version = "1.0", id = currency.Id }, currency);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("already exists"))
        {
            _logger.LogWarning(ex, "Currency already exists: {Code}", request.Code);

            return Conflict(new ErrorResponse
            {
                Error = "Conflict",
                Message = ex.Message,
                Timestamp = DateTime.UtcNow,
                CorrelationId = HttpContext.TraceIdentifier
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating currency {Code}", request.Code);

            return StatusCode(StatusCodes.Status500InternalServerError, new ErrorResponse
            {
                Error = "InternalServerError",
                Message = "An error occurred while creating currency",
                Timestamp = DateTime.UtcNow,
                CorrelationId = HttpContext.TraceIdentifier
            });
        }
    }

    /// <summary>
    /// Update an existing currency (admin only)
    /// </summary>
    /// <param name="code">Currency code (ISO 4217)</param>
    /// <param name="request">Currency update request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Updated currency</returns>
    [HttpPut("{code}")]
    [Authorize] // Requires JWT authentication
    [RequirePermission(CurrencyPermissions.CurrenciesUpdate)]
    [EnableRateLimiting("AuthenticatedApi")]
    [ProducesResponseType(typeof(CurrencyResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<CurrencyResponse>> Update(
        string code,
        [FromBody] UpdateCurrencyRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("PUT /v1/currencies/{Code} - Updating currency", code);

            // Validate request
            var validationErrors = new List<string>();

            if (string.IsNullOrWhiteSpace(request.Symbol))
                validationErrors.Add("Symbol is required");

            if (string.IsNullOrWhiteSpace(request.Name))
                validationErrors.Add("Name is required");

            if (request.DecimalPlaces < 0)
                validationErrors.Add("DecimalPlaces must be non-negative");

            if (validationErrors.Count > 0)
            {
                return BadRequest(new ErrorResponse
                {
                    Error = "BadRequest",
                    Message = "Invalid currency update request",
                    Timestamp = DateTime.UtcNow,
                    CorrelationId = HttpContext.TraceIdentifier,
                    Details = new Dictionary<string, string[]>
                    {
                        { "validation", validationErrors.ToArray() }
                    }
                });
            }

            var currency = await _currencyService.UpdateAsync(code, request, cancellationToken);

            if (currency == null)
            {
                return NotFound(new ErrorResponse
                {
                    Error = "NotFound",
                    Message = $"Currency '{code}' not found",
                    Timestamp = DateTime.UtcNow,
                    CorrelationId = HttpContext.TraceIdentifier
                });
            }

            Response.Headers["X-Correlation-ID"] = HttpContext.TraceIdentifier;

            return Ok(currency);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            _logger.LogWarning(ex, "Concurrency conflict updating currency {Code}", code);

            return Conflict(new ErrorResponse
            {
                Error = "Conflict",
                Message = "Currency has been modified by another user. Please refresh and try again.",
                Timestamp = DateTime.UtcNow,
                CorrelationId = HttpContext.TraceIdentifier
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating currency {Code}", code);

            return StatusCode(StatusCodes.Status500InternalServerError, new ErrorResponse
            {
                Error = "InternalServerError",
                Message = "An error occurred while updating currency",
                Timestamp = DateTime.UtcNow,
                CorrelationId = HttpContext.TraceIdentifier
            });
        }
    }

    /// <summary>
    /// Update currency by ID with optimistic concurrency (admin only)
    /// </summary>
    /// <param name="id">Currency GUID</param>
    /// <param name="request">Currency update request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Updated currency</returns>
    [HttpPut("~/currency/v{version:apiVersion}/admin/currencies/{id:guid}")]
    [Authorize] // Requires JWT authentication
    [RequirePermission(CurrencyPermissions.CurrenciesUpdate)]
    [EnableRateLimiting("AuthenticatedApi")]
    [ProducesResponseType(typeof(CurrencyResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status412PreconditionFailed)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<CurrencyResponse>> UpdateById(
        Guid id,
        [FromBody] UpdateCurrencyRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // FR-006: Check If-Match header for optimistic concurrency
            if (!Request.Headers.IfMatch.Any())
            {
                return StatusCode(StatusCodes.Status412PreconditionFailed, new ErrorResponse
                {
                    Error = "PreconditionFailed",
                    Message = "If-Match header is required for currency updates",
                    Timestamp = DateTime.UtcNow,
                    CorrelationId = HttpContext.TraceIdentifier
                });
            }

            var clientETag = Request.Headers.IfMatch.First()?.Trim('"');

            _logger.LogInformation("PUT /v1/admin/currencies/{Id} - Updating currency", id);

            // Get current currency to verify ETag
            var currentCurrency = await _currencyService.GetByIdAsync(id, cancellationToken);
            if (currentCurrency == null)
            {
                return NotFound(new ErrorResponse
                {
                    Error = "NotFound",
                    Message = $"Currency with ID '{id}' not found",
                    Timestamp = DateTime.UtcNow,
                    CorrelationId = HttpContext.TraceIdentifier
                });
            }

            // Verify ETag matches current version (FR-006)
            var currentETag = GenerateETag(currentCurrency);
            if (clientETag != currentETag)
            {
                return StatusCode(StatusCodes.Status412PreconditionFailed, new ErrorResponse
                {
                    Error = "PreconditionFailed",
                    Message = "Currency has been modified by another user. Please refresh and try again.",
                    Timestamp = DateTime.UtcNow,
                    CorrelationId = HttpContext.TraceIdentifier
                });
            }

            // Validate request
            var validationErrors = new List<string>();

            if (string.IsNullOrWhiteSpace(request.Symbol))
                validationErrors.Add("Symbol is required");

            if (string.IsNullOrWhiteSpace(request.Name))
                validationErrors.Add("Name is required");

            if (request.DecimalPlaces < 0)
                validationErrors.Add("DecimalPlaces must be non-negative");

            if (validationErrors.Count > 0)
            {
                return BadRequest(new ErrorResponse
                {
                    Error = "BadRequest",
                    Message = "Invalid currency update request",
                    Timestamp = DateTime.UtcNow,
                    CorrelationId = HttpContext.TraceIdentifier,
                    Details = new Dictionary<string, string[]>
                    {
                        { "validation", validationErrors.ToArray() }
                    }
                });
            }

            var currency = await _currencyService.UpdateByIdAsync(id, request, cancellationToken);

            if (currency == null)
            {
                return NotFound(new ErrorResponse
                {
                    Error = "NotFound",
                    Message = $"Currency with ID '{id}' not found",
                    Timestamp = DateTime.UtcNow,
                    CorrelationId = HttpContext.TraceIdentifier
                });
            }

            // Generate new ETag
            var newETag = GenerateETag(currency);
            Response.Headers.ETag = $"\"{newETag}\"";
            Response.Headers["X-Correlation-ID"] = HttpContext.TraceIdentifier;

            return Ok(currency);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            _logger.LogWarning(ex, "Concurrency conflict updating currency {Id}", id);

            return StatusCode(StatusCodes.Status412PreconditionFailed, new ErrorResponse
            {
                Error = "PreconditionFailed",
                Message = "Currency has been modified by another user. Please refresh and try again.",
                Timestamp = DateTime.UtcNow,
                CorrelationId = HttpContext.TraceIdentifier
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating currency {Id}", id);

            return StatusCode(StatusCodes.Status500InternalServerError, new ErrorResponse
            {
                Error = "InternalServerError",
                Message = "An error occurred while updating currency",
                Timestamp = DateTime.UtcNow,
                CorrelationId = HttpContext.TraceIdentifier
            });
        }
    }

    /// <summary>
    /// Delete a currency by ID (admin only)
    /// </summary>
    /// <param name="id">Currency GUID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Success indicator</returns>
    [HttpDelete("~/currency/v{version:apiVersion}/admin/currencies/{id:guid}")]
    [Authorize] // Requires JWT authentication
    [RequirePermission(CurrencyPermissions.CurrenciesDelete)]
    [EnableRateLimiting("AuthenticatedApi")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> DeleteById(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("DELETE /v1/admin/currencies/{Id} - Deleting currency", id);

            var deleted = await _currencyService.DeleteByIdAsync(id, cancellationToken);

            if (!deleted)
            {
                return NotFound(new ErrorResponse
                {
                    Error = "NotFound",
                    Message = $"Currency with ID '{id}' not found",
                    Timestamp = DateTime.UtcNow,
                    CorrelationId = HttpContext.TraceIdentifier
                });
            }

            Response.Headers["X-Correlation-ID"] = HttpContext.TraceIdentifier;

            return NoContent();
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("dependencies") || ex.Message.Contains("country mappings"))
        {
            _logger.LogWarning(ex, "Cannot delete currency {Id} due to dependencies", id);

            return Conflict(new ErrorResponse
            {
                Error = "Conflict",
                Message = ex.Message,
                Timestamp = DateTime.UtcNow,
                CorrelationId = HttpContext.TraceIdentifier
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting currency {Id}", id);

            return StatusCode(StatusCodes.Status500InternalServerError, new ErrorResponse
            {
                Error = "InternalServerError",
                Message = "An error occurred while deleting currency",
                Timestamp = DateTime.UtcNow,
                CorrelationId = HttpContext.TraceIdentifier
            });
        }
    }

    /// <summary>
    /// Delete a currency (soft delete, admin only)
    /// </summary>
    /// <param name="code">Currency code (ISO 4217)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Success indicator</returns>
    [HttpDelete("{code}")]
    [Authorize] // Requires JWT authentication
    [RequirePermission(CurrencyPermissions.CurrenciesDelete)]
    [EnableRateLimiting("AuthenticatedApi")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Delete(
        string code,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("DELETE /v1/currencies/{Code} - Deleting currency", code);

            var deleted = await _currencyService.DeleteAsync(code, cancellationToken);

            if (!deleted)
            {
                return NotFound(new ErrorResponse
                {
                    Error = "NotFound",
                    Message = $"Currency '{code}' not found",
                    Timestamp = DateTime.UtcNow,
                    CorrelationId = HttpContext.TraceIdentifier
                });
            }

            Response.Headers["X-Correlation-ID"] = HttpContext.TraceIdentifier;

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting currency {Code}", code);

            return StatusCode(StatusCodes.Status500InternalServerError, new ErrorResponse
            {
                Error = "InternalServerError",
                Message = "An error occurred while deleting currency",
                Timestamp = DateTime.UtcNow,
                CorrelationId = HttpContext.TraceIdentifier
            });
        }
    }

    /// <summary>
    /// Activate a currency (admin only)
    /// </summary>
    [HttpPost("~/currency/v{version:apiVersion}/admin/currencies/{id:guid}/activate")]
    [Authorize]
    [RequirePermission(CurrencyPermissions.CurrenciesActivate)]
    [EnableRateLimiting("AuthenticatedApi")]
    public async Task<IActionResult> Activate(Guid id, CancellationToken cancellationToken = default)
    {
        var success = await _currencyService.ActivateAsync(id, cancellationToken);
        if (!success) return NotFound();
        return Ok();
    }

    /// <summary>
    /// Deactivate a currency (admin only)
    /// </summary>
    [HttpPost("~/currency/v{version:apiVersion}/admin/currencies/{id:guid}/deactivate")]
    [Authorize]
    [RequirePermission(CurrencyPermissions.CurrenciesActivate)]
    [EnableRateLimiting("AuthenticatedApi")]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken cancellationToken = default)
    {
        var success = await _currencyService.DeactivateAsync(id, cancellationToken);
        if (!success) return NotFound();
        return Ok();
    }

    /// <summary>
    /// Generates ETag for response caching
    /// </summary>
    /// <remarks>
    /// Per contracts/openapi.yaml: ETags enable conditional requests (If-None-Match).
    /// Uses SHA256 hash of JSON content for deterministic ETags.
    /// </remarks>
    private static string GenerateETag(object content)
    {
        var json = JsonSerializer.Serialize(content);
        var bytes = Encoding.UTF8.GetBytes(json);
        var hash = SHA256.HashData(bytes);
        return Convert.ToBase64String(hash)[..16]; // First 16 chars of base64 hash
    }
}
