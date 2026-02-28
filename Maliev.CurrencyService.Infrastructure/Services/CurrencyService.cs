using Maliev.Aspire.ServiceDefaults.Caching;
using Maliev.CurrencyService.Application.DTOs.Currencies;
using Maliev.CurrencyService.Application.Interfaces;
using Maliev.CurrencyService.Domain.Entities;
using Maliev.CurrencyService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Maliev.CurrencyService.Infrastructure.Services;

/// <summary>
/// Currency metadata service implementation with caching.
/// </summary>
/// <remarks>
/// Uses two-tier caching (in-process + Redis) for currency metadata.
/// Cache TTL: 300 seconds (5 minutes) for currency list, 3600 seconds (1 hour) for country mapping.
/// </remarks>
public class CurrencyService : ICurrencyService
{
    private readonly CurrencyDbContext _context;
    private readonly ICacheService _cacheService;
    private readonly ILogger<CurrencyService> _logger;

    private const string CurrencyListCacheKeyPrefix = "currency:list";
    private const string CountryCurrencyCacheKeyPrefix = "country:currency";
    private const int CurrencyListCacheTtlSeconds = 300;
    private const int CountryCurrencyCacheTtlSeconds = 3600;

    /// <summary>
    /// Initializes a new instance of the <see cref="CurrencyService"/> class.
    /// </summary>
    /// <param name="context">The database context.</param>
    /// <param name="cacheService">The cache service.</param>
    /// <param name="logger">The logger.</param>
    public CurrencyService(
        CurrencyDbContext context,
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
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);

        var cacheKey = $"{CurrencyListCacheKeyPrefix}:page{page}:size{pageSize}:active{isActive?.ToString() ?? "all"}";

        var cachedResponse = await _cacheService.GetAsync<PaginatedCurrencyResponse>(cacheKey, cancellationToken);
        if (cachedResponse != null)
        {
            _logger.LogDebug("Currency list cache hit for key: {CacheKey}", cacheKey);
            return cachedResponse;
        }

        _logger.LogDebug("Currency list cache miss for key: {CacheKey}", cacheKey);

        var query = _context.Currencies.AsNoTracking();

        if (isActive.HasValue)
        {
            query = query.Where(c => c.IsActive == isActive.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

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
    /// <param name="countryCode">The ISO2 or ISO3 country code.</param>
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
        var cacheKey = $"{CountryCurrencyCacheKeyPrefix}:{normalizedCode}";

        var cachedResponse = await _cacheService.GetAsync<CurrencyResponse>(cacheKey, cancellationToken);
        if (cachedResponse != null)
        {
            _logger.LogDebug("Country currency cache hit for {CountryCode}", normalizedCode);
            return cachedResponse;
        }

        _logger.LogDebug("Country currency cache miss for {CountryCode}", normalizedCode);

        var isIso2 = normalizedCode.Length == 2;
        var isIso3 = normalizedCode.Length == 3;

        if (!isIso2 && !isIso3)
        {
            _logger.LogWarning("Invalid country code format: {CountryCode} (must be 2 or 3 characters)", countryCode);
            return null;
        }

        var countryCurrency = await _context.CountryCurrencies
            .AsNoTracking()
            .Where(cc => isIso2 ? cc.CountryIso2 == normalizedCode : cc.CountryIso3 == normalizedCode)
            .Where(cc => cc.IsPrimary)
            .FirstOrDefaultAsync(cancellationToken);

        if (countryCurrency == null)
        {
            _logger.LogWarning("No currency mapping found for country code: {CountryCode}", normalizedCode);
            return null;
        }

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
    /// <param name="code">The 3-letter ISO 4217 currency code.</param>
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

        return await _context.Currencies
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
            IsPrimary = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Currencies.Add(currency);
        await _context.SaveChangesAsync(cancellationToken);

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

        var currency = await _context.Currencies
            .Where(c => c.Code == normalizedCode)
            .FirstOrDefaultAsync(cancellationToken);

        if (currency == null)
        {
            _logger.LogWarning("Currency not found: {Code}", normalizedCode);
            return null;
        }

        if (request.Name != null)
            currency.Name = request.Name;

        if (request.Symbol != null)
            currency.Symbol = request.Symbol;

        if (request.DecimalPlaces.HasValue)
            currency.DecimalPlaces = request.DecimalPlaces.Value;

        if (request.IsActive.HasValue)
            currency.IsActive = request.IsActive.Value;

        if (request.Version != null)
        {
            _context.Entry(currency).Property("Version").OriginalValue = request.Version;
        }

        currency.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

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

        currency.IsActive = false;
        currency.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

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

        return await _context.Currencies
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

        var currency = await _context.Currencies
            .Where(c => c.Id == id)
            .FirstOrDefaultAsync(cancellationToken);

        if (currency == null)
        {
            _logger.LogWarning("Currency not found with ID: {Id}", id);
            return null;
        }

        if (!string.IsNullOrWhiteSpace(request.Symbol))
            currency.Symbol = request.Symbol;

        if (!string.IsNullOrWhiteSpace(request.Name))
            currency.Name = request.Name;

        if (request.DecimalPlaces.HasValue)
            currency.DecimalPlaces = request.DecimalPlaces.Value;

        if (request.Version != null)
        {
            _context.Entry(currency).Property("Version").OriginalValue = request.Version;
        }

        currency.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

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
    /// Soft deletes a currency by its unique identifier.
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

        currency.IsActive = false;
        currency.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        await InvalidateCurrencyListCacheAsync(cancellationToken);
        await InvalidateCountryCacheForCurrencyAsync(currency.Code, cancellationToken);

        _logger.LogInformation("Deleted (soft) currency by ID: {Id} ({Code})", id, currency.Code);

        return true;
    }

    /// <summary>
    /// Activates a currency by ID.
    /// </summary>
    /// <param name="id">The GUID of the currency to activate.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>True if activated, otherwise false.</returns>
    public async Task<bool> ActivateAsync(Guid id, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Activating currency by ID: {Id}", id);

        var currency = await _context.Currencies
            .Where(c => c.Id == id)
            .FirstOrDefaultAsync(cancellationToken);

        if (currency == null)
        {
            _logger.LogWarning("Currency not found with ID: {Id}", id);
            return false;
        }

        currency.IsActive = true;
        currency.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);
        await InvalidateCurrencyListCacheAsync(cancellationToken);
        await InvalidateCountryCacheForCurrencyAsync(currency.Code, cancellationToken);

        return true;
    }

    /// <summary>
    /// Deactivates a currency by ID.
    /// </summary>
    /// <param name="id">The GUID of the currency to deactivate.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>True if deactivated, otherwise false.</returns>
    public async Task<bool> DeactivateAsync(Guid id, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Deactivating currency by ID: {Id}", id);

        var currency = await _context.Currencies
            .Where(c => c.Id == id)
            .FirstOrDefaultAsync(cancellationToken);

        if (currency == null)
        {
            _logger.LogWarning("Currency not found with ID: {Id}", id);
            return false;
        }

        currency.IsActive = false;
        currency.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);
        await InvalidateCurrencyListCacheAsync(cancellationToken);
        await InvalidateCountryCacheForCurrencyAsync(currency.Code, cancellationToken);

        return true;
    }

    private async Task InvalidateCurrencyListCacheAsync(CancellationToken cancellationToken)
    {
        await _cacheService.RemoveByPatternAsync($"{CurrencyListCacheKeyPrefix}:*", cancellationToken);
        _logger.LogDebug("Invalidated currency list cache");
    }

    private async Task InvalidateCountryCacheForCurrencyAsync(string currencyCode, CancellationToken cancellationToken)
    {
        await _cacheService.RemoveByPatternAsync($"{CountryCurrencyCacheKeyPrefix}:*", cancellationToken);
        _logger.LogDebug("Invalidated country currency cache for {CurrencyCode}", currencyCode);
    }
}
