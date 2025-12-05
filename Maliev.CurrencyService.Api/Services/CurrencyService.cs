using Maliev.CurrencyService.Api.Models.Currencies;
using Maliev.CurrencyService.Data;
using Maliev.CurrencyService.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace Maliev.CurrencyService.Api.Services;

/// <summary>
/// Currency metadata service implementation with caching
/// </summary>
/// <remarks>
/// Per research.md decision 3: Uses two-tier caching (in-process + Redis) for currency metadata.
/// Cache TTL: 300 seconds (5 minutes) for currency list, 3600 seconds (1 hour) for country mapping.
/// </remarks>
public class CurrencyService : ICurrencyService
{
    private readonly CurrencyServiceDbContext _context;
    private readonly ICacheService _cacheService;
    private readonly ILogger<CurrencyService> _logger;

    private const string CurrencyListCacheKeyPrefix = "currency:list";
    private const string CountryCurrencyCacheKeyPrefix = "country:currency";
    private const int CurrencyListCacheTtlSeconds = 300; // 5 minutes
    private const int CountryCurrencyCacheTtlSeconds = 3600; // 1 hour

    /// <summary>
    /// Initializes a new instance of the <see cref="CurrencyService"/> class.
    /// </summary>
    /// <param name="context">The database context.</param>
    /// <param name="cacheService">The cache service.</param>
    /// <param name="logger">The logger.</param>
    public CurrencyService(
        CurrencyServiceDbContext context,
        ICacheService cacheService,
        ILogger<CurrencyService> logger)
    {
        _context = context;
        _cacheService = cacheService;
        _logger = logger;
    }

    /// <summary>
    /// Retrieves a paginated list of currencies.
    /// </summary>
    /// <param name="page">The page number (1-indexed).</param>
    /// <param name="pageSize">The number of items per page.</param>
    /// <param name="isActive">Optional filter to retrieve only active or inactive currencies.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A paginated response containing currency details.</returns>
    public async Task<PaginatedCurrencyResponse> GetAllAsync(
        int page = 1,
        int pageSize = 50,
        bool? isActive = null,
        CancellationToken cancellationToken = default)
    {
        // Validate pagination parameters
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);

        // Generate cache key
        var cacheKey = $"{CurrencyListCacheKeyPrefix}:page{page}:size{pageSize}:active{isActive?.ToString() ?? "all"}";

        // Try to get from cache
        var cachedResponse = await _cacheService.GetAsync<PaginatedCurrencyResponse>(cacheKey, cancellationToken);
        if (cachedResponse != null)
        {
            _logger.LogDebug("Currency list cache hit for key: {CacheKey}", cacheKey);
            return cachedResponse;
        }

        _logger.LogDebug("Currency list cache miss for key: {CacheKey}", cacheKey);

        // Build query
        var query = _context.Currencies.AsNoTracking();

        // Apply active filter if specified
        if (isActive.HasValue)
        {
            query = query.Where(c => c.IsActive == isActive.Value);
        }

        // Get total count
        var totalCount = await query.CountAsync(cancellationToken);

        // Calculate total pages
        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

        // Apply pagination and ordering (by code for consistency)
        var currencies = await query
            .OrderBy(c => c.Code)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(c => new CurrencyResponse
            {
                Id = c.Id,
                Code = c.Code,
                Symbol = c.Symbol,
                Name = c.Name,
                DecimalPlaces = c.DecimalPlaces,
                IsActive = c.IsActive,
                IsPrimary = c.IsPrimary,
                CreatedAt = c.CreatedAt,
                UpdatedAt = c.UpdatedAt
            })
            .ToListAsync(cancellationToken);

        var response = new PaginatedCurrencyResponse
        {
            Items = currencies,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            TotalPages = totalPages
        };

        // Cache the response
        await _cacheService.SetAsync(
            cacheKey,
            response,
            TimeSpan.FromSeconds(CurrencyListCacheTtlSeconds),
            cancellationToken);

        _logger.LogInformation("Retrieved {Count} currencies for page {Page} (total: {TotalCount})",
            currencies.Count, page, totalCount);

        return response;
    }

    /// <summary>
    /// Retrieves a currency by its country code (ISO2 or ISO3).
    /// </summary>
    /// <param name="countryCode">The ISO2 or ISO3 country code (e.g., "TH" or "THA").</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The currency response if found, otherwise null.</returns>
    public async Task<CurrencyResponse?> GetByCountryCodeAsync(
        string countryCode,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(countryCode))
        {
            _logger.LogWarning("GetByCountryCodeAsync called with null or empty country code");
            return null;
        }

        var normalizedCode = countryCode.ToUpperInvariant();

        // Generate cache key
        var cacheKey = $"{CountryCurrencyCacheKeyPrefix}:{normalizedCode}";

        // Try to get from cache
        var cachedResponse = await _cacheService.GetAsync<CurrencyResponse>(cacheKey, cancellationToken);
        if (cachedResponse != null)
        {
            _logger.LogDebug("Country currency cache hit for {CountryCode}", normalizedCode);
            return cachedResponse;
        }

        _logger.LogDebug("Country currency cache miss for {CountryCode}", normalizedCode);

        // Determine if ISO2 or ISO3 based on length
        var isIso2 = normalizedCode.Length == 2;
        var isIso3 = normalizedCode.Length == 3;

        if (!isIso2 && !isIso3)
        {
            _logger.LogWarning("Invalid country code format: {CountryCode} (must be 2 or 3 characters)",
                countryCode);
            return null;
        }

        // Query country currency mapping
        var countryCurrency = await _context.CountryCurrencies
            .AsNoTracking()
            .Where(cc => isIso2 ? cc.CountryIso2 == normalizedCode : cc.CountryIso3 == normalizedCode)
            .Where(cc => cc.IsPrimary) // Only primary currency for the country
            .FirstOrDefaultAsync(cancellationToken);

        if (countryCurrency == null)
        {
            _logger.LogWarning("No currency mapping found for country code: {CountryCode}", normalizedCode);
            return null;
        }

        // Get the currency details
        var currency = await _context.Currencies
            .AsNoTracking()
            .Where(c => c.Code == countryCurrency.CurrencyCode)
            .Select(c => new CurrencyResponse
            {
                Id = c.Id,
                Code = c.Code,
                Symbol = c.Symbol,
                Name = c.Name,
                DecimalPlaces = c.DecimalPlaces,
                IsActive = c.IsActive,
                IsPrimary = c.IsPrimary,
                CreatedAt = c.CreatedAt,
                UpdatedAt = c.UpdatedAt
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (currency == null)
        {
            _logger.LogError("Currency {CurrencyCode} not found for country {CountryCode}",
                countryCurrency.CurrencyCode, normalizedCode);
            return null;
        }

        // Cache the response (longer TTL for country mapping - 1 hour)
        await _cacheService.SetAsync(
            cacheKey,
            currency,
            TimeSpan.FromSeconds(CountryCurrencyCacheTtlSeconds),
            cancellationToken);

        _logger.LogInformation("Resolved country {CountryCode} to currency {CurrencyCode}",
            normalizedCode, currency.Code);

        return currency;
    }

    /// <summary>
    /// Retrieves a currency by its ISO 4217 code.
    /// </summary>
    /// <param name="code">The 3-letter ISO 4217 currency code (e.g., "USD").</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The currency response if found, otherwise null.</returns>
    public async Task<CurrencyResponse?> GetByCodeAsync(
        string code,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            _logger.LogWarning("GetByCodeAsync called with null or empty code");
            return null;
        }

        var normalizedCode = code.ToUpperInvariant();

        var currency = await _context.Currencies
            .AsNoTracking()
            .Where(c => c.Code == normalizedCode)
            .Select(c => new CurrencyResponse
            {
                Id = c.Id,
                Code = c.Code,
                Symbol = c.Symbol,
                Name = c.Name,
                DecimalPlaces = c.DecimalPlaces,
                IsActive = c.IsActive,
                IsPrimary = c.IsPrimary,
                CreatedAt = c.CreatedAt,
                UpdatedAt = c.UpdatedAt
            })
            .FirstOrDefaultAsync(cancellationToken);

        return currency;
    }

    /// <summary>
    /// Creates a new currency.
    /// </summary>
    /// <param name="request">The request containing the new currency data.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The created currency response.</returns>
    /// <exception cref="InvalidOperationException">Thrown if a currency with the same code already exists.</exception>
    public async Task<CurrencyResponse> CreateAsync(
        CreateCurrencyRequest request,
        CancellationToken cancellationToken = default)
    {
        var normalizedCode = request.Code.ToUpperInvariant();

        _logger.LogInformation("Creating currency: {Code}", normalizedCode);

        // Check if currency already exists
        var exists = await _context.Currencies
            .AnyAsync(c => c.Code == normalizedCode, cancellationToken);

        if (exists)
        {
            throw new InvalidOperationException($"Currency {normalizedCode} already exists");
        }

        var currency = new Currency
        {
            Code = normalizedCode,
            Name = request.Name,
            Symbol = request.Symbol,
            DecimalPlaces = request.DecimalPlaces,
            IsActive = request.IsActive,
            IsPrimary = false, // Primary currencies are managed separately
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Currencies.Add(currency);
        await _context.SaveChangesAsync(cancellationToken);

        // Invalidate currency list cache
        await InvalidateCurrencyListCacheAsync(cancellationToken);

        _logger.LogInformation("Created currency: {Code}", normalizedCode);

        return new CurrencyResponse
        {
            Id = currency.Id,
            Code = currency.Code,
            Symbol = currency.Symbol,
            Name = currency.Name,
            DecimalPlaces = currency.DecimalPlaces,
            IsActive = currency.IsActive,
            IsPrimary = currency.IsPrimary,
            CreatedAt = currency.CreatedAt,
            UpdatedAt = currency.UpdatedAt
        };
    }

    /// <summary>
    /// Updates an existing currency by its code.
    /// </summary>
    /// <param name="code">The ISO 4217 code of the currency to update.</param>
    /// <param name="request">The request containing the updated currency data.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The updated currency response if found, otherwise null.</returns>
    public async Task<CurrencyResponse?> UpdateAsync(
        string code,
        UpdateCurrencyRequest request,
        CancellationToken cancellationToken = default)
    {
        var normalizedCode = code.ToUpperInvariant();

        _logger.LogInformation("Updating currency: {Code}", normalizedCode);

        // Get currency with tracking for update
        var currency = await _context.Currencies
            .Where(c => c.Code == normalizedCode)
            .FirstOrDefaultAsync(cancellationToken);

        if (currency == null)
        {
            _logger.LogWarning("Currency not found: {Code}", normalizedCode);
            return null;
        }

        // Note: Optimistic concurrency is handled via ETag/If-Match header in UpdateByIdAsync
        // This method (UpdateAsync by code) doesn't enforce version checking

        // Apply updates (only non-null fields)
        if (request.Name != null)
            currency.Name = request.Name;

        if (request.Symbol != null)
            currency.Symbol = request.Symbol;

        if (request.DecimalPlaces.HasValue)
            currency.DecimalPlaces = request.DecimalPlaces.Value;

        if (request.IsActive.HasValue)
            currency.IsActive = request.IsActive.Value;

        currency.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        // Invalidate caches
        await InvalidateCurrencyListCacheAsync(cancellationToken);
        await InvalidateCountryCacheForCurrencyAsync(normalizedCode, cancellationToken);

        _logger.LogInformation("Updated currency: {Code}", normalizedCode);

        return new CurrencyResponse
        {
            Id = currency.Id,
            Code = currency.Code,
            Symbol = currency.Symbol,
            Name = currency.Name,
            DecimalPlaces = currency.DecimalPlaces,
            IsActive = currency.IsActive,
            IsPrimary = currency.IsPrimary,
            CreatedAt = currency.CreatedAt,
            UpdatedAt = currency.UpdatedAt
        };
    }

    /// <summary>
    /// Soft deletes a currency by setting its IsActive flag to false.
    /// </summary>
    /// <param name="code">The ISO 4217 code of the currency to delete.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>True if the currency was found and soft deleted, otherwise false.</returns>
    public async Task<bool> DeleteAsync(
        string code,
        CancellationToken cancellationToken = default)
    {
        var normalizedCode = code.ToUpperInvariant();

        _logger.LogInformation("Deleting (soft) currency: {Code}", normalizedCode);

        var currency = await _context.Currencies
            .Where(c => c.Code == normalizedCode)
            .FirstOrDefaultAsync(cancellationToken);

        if (currency == null)
        {
            _logger.LogWarning("Currency not found: {Code}", normalizedCode);
            return false;
        }

        // Soft delete
        currency.IsActive = false;
        currency.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        // Invalidate caches
        await InvalidateCurrencyListCacheAsync(cancellationToken);
        await InvalidateCountryCacheForCurrencyAsync(normalizedCode, cancellationToken);

        _logger.LogInformation("Deleted (soft) currency: {Code}", normalizedCode);

        return true;
    }

    /// <summary>
    /// Retrieves a currency by its unique identifier.
    /// </summary>
    /// <param name="id">The GUID of the currency.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The currency response if found, otherwise null.</returns>
    public async Task<CurrencyResponse?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Getting currency by ID: {Id}", id);

        var currency = await _context.Currencies
            .AsNoTracking()
            .Where(c => c.Id == id && c.IsActive)
            .Select(c => new CurrencyResponse
            {
                Id = c.Id,
                Code = c.Code,
                Symbol = c.Symbol,
                Name = c.Name,
                DecimalPlaces = c.DecimalPlaces,
                IsActive = c.IsActive,
                IsPrimary = c.IsPrimary,
                CreatedAt = c.CreatedAt,
                UpdatedAt = c.UpdatedAt
            })
            .FirstOrDefaultAsync(cancellationToken);

        return currency;
    }

    /// <summary>
    /// Updates an existing currency by its unique identifier.
    /// </summary>
    /// <param name="id">The GUID of the currency to update.</param>
    /// <param name="request">The request containing the updated currency data.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The updated currency response if found, otherwise null.</returns>
    public async Task<CurrencyResponse?> UpdateByIdAsync(
        Guid id,
        UpdateCurrencyRequest request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Updating currency by ID: {Id}", id);

        // Get currency with tracking for update
        var currency = await _context.Currencies
            .Where(c => c.Id == id)
            .FirstOrDefaultAsync(cancellationToken);

        if (currency == null)
        {
            _logger.LogWarning("Currency not found with ID: {Id}", id);
            return null;
        }

        // Apply updates
        if (!string.IsNullOrWhiteSpace(request.Symbol))
            currency.Symbol = request.Symbol;

        if (!string.IsNullOrWhiteSpace(request.Name))
            currency.Name = request.Name;

        if (request.DecimalPlaces.HasValue)
            currency.DecimalPlaces = request.DecimalPlaces.Value;

        currency.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        // Invalidate caches
        await InvalidateCurrencyListCacheAsync(cancellationToken);
        await InvalidateCountryCacheForCurrencyAsync(currency.Code, cancellationToken);

        _logger.LogInformation("Updated currency by ID: {Id} ({Code})", id, currency.Code);

        return new CurrencyResponse
        {
            Id = currency.Id,
            Code = currency.Code,
            Symbol = currency.Symbol,
            Name = currency.Name,
            DecimalPlaces = currency.DecimalPlaces,
            IsActive = currency.IsActive,
            IsPrimary = currency.IsPrimary,
            CreatedAt = currency.CreatedAt,
            UpdatedAt = currency.UpdatedAt
        };
    }

    /// <summary>
    /// Soft deletes a currency by its unique identifier, setting its IsActive flag to false.
    /// </summary>
    /// <param name="id">The GUID of the currency to delete.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>True if the currency was found and soft deleted, otherwise false.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the currency has existing country mappings.</exception>
    public async Task<bool> DeleteByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Deleting (soft) currency by ID: {Id}", id);

        var currency = await _context.Currencies
            .Where(c => c.Id == id)
            .FirstOrDefaultAsync(cancellationToken);

        if (currency == null)
        {
            _logger.LogWarning("Currency not found with ID: {Id}", id);
            return false;
        }

        // Check for dependencies (country mappings)
        var hasCountryMappings = await _context.CountryCurrencies
            .AnyAsync(cc => cc.CurrencyCode == currency.Code, cancellationToken);

        if (hasCountryMappings)
        {
            _logger.LogWarning("Cannot delete currency {Code} (ID: {Id}) - has country mappings",
                currency.Code, id);
            throw new InvalidOperationException(
                $"Cannot delete currency {currency.Code} as it has dependencies (country mappings). " +
                "Remove country mappings first or deactivate the currency instead.");
        }

        // Soft delete
        currency.IsActive = false;
        currency.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        // Invalidate caches
        await InvalidateCurrencyListCacheAsync(cancellationToken);
        await InvalidateCountryCacheForCurrencyAsync(currency.Code, cancellationToken);

        _logger.LogInformation("Deleted (soft) currency by ID: {Id} ({Code})", id, currency.Code);

        return true;
    }

    /// <summary>
    /// Invalidates all currency list cache keys
    /// </summary>
    private async Task InvalidateCurrencyListCacheAsync(CancellationToken cancellationToken)
    {
        // Use pattern-based cache invalidation
        await _cacheService.RemoveByPatternAsync($"{CurrencyListCacheKeyPrefix}:*", cancellationToken);
        _logger.LogDebug("Invalidated currency list cache");
    }

    /// <summary>
    /// Invalidates country-to-currency cache for a specific currency
    /// </summary>
    private async Task InvalidateCountryCacheForCurrencyAsync(string currencyCode, CancellationToken cancellationToken)
    {
        // This would require knowing which countries use this currency
        // For simplicity, we'll invalidate all country caches
        await _cacheService.RemoveByPatternAsync($"{CountryCurrencyCacheKeyPrefix}:*", cancellationToken);
        _logger.LogDebug("Invalidated country currency cache for {CurrencyCode}", currencyCode);
    }
}
