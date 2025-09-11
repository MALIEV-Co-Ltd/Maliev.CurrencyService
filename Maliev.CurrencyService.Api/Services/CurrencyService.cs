using Maliev.CurrencyService.Api.Models;
using Maliev.CurrencyService.Data.DbContexts;
using Maliev.CurrencyService.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Maliev.CurrencyService.Api.Services;

public class CurrencyService : ICurrencyService
{
    private readonly CurrencyDbContext _context;
    private readonly IMemoryCache _cache;
    private readonly ILogger<CurrencyService> _logger;
    private readonly CacheOptions _cacheOptions;

    public CurrencyService(
        CurrencyDbContext context,
        IMemoryCache cache,
        ILogger<CurrencyService> logger,
        CacheOptions cacheOptions)
    {
        _context = context;
        _cache = cache;
        _logger = logger;
        _cacheOptions = cacheOptions;
    }

    public async Task<CurrencyDto?> GetByIdAsync(int id)
    {
        var cacheKey = $"currency_id_{id}";
        
        if (_cache.TryGetValue(cacheKey, out CurrencyDto? cachedCurrency))
        {
            _logger.LogDebug("Cache hit for currency ID: {Id}", id);
            return cachedCurrency;
        }

        _logger.LogDebug("Cache miss for currency ID: {Id}", id);
        
        var currency = await _context.Currencies
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id);

        if (currency == null)
        {
            return null;
        }

        var dto = MapToDto(currency);
        
        _cache.Set(cacheKey, dto, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(_cacheOptions.CurrencyCacheDurationMinutes),
            Size = 1
        });

        return dto;
    }

    public async Task<CurrencyDto?> GetByShortNameAsync(string shortName)
    {
        var cacheKey = $"currency_code_{shortName.ToUpperInvariant()}";
        
        if (_cache.TryGetValue(cacheKey, out CurrencyDto? cachedCurrency))
        {
            _logger.LogDebug("Cache hit for currency code: {Code}", shortName);
            return cachedCurrency;
        }

        _logger.LogDebug("Cache miss for currency code: {Code}", shortName);
        
        var currency = await _context.Currencies
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.ShortName == shortName.ToUpperInvariant());

        if (currency == null)
        {
            return null;
        }

        var dto = MapToDto(currency);
        
        _cache.Set(cacheKey, dto, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(_cacheOptions.CurrencyCacheDurationMinutes),
            Size = 1
        });

        return dto;
    }

    public async Task<PagedResult<CurrencyDto>> GetAllAsync(int page = 1, int pageSize = 20, string? search = null)
    {
        var cacheKey = $"currency_list_{page}_{pageSize}_{search ?? "all"}";
        
        if (_cache.TryGetValue(cacheKey, out PagedResult<CurrencyDto>? cachedResult))
        {
            _logger.LogDebug("Cache hit for currency list: {CacheKey}", cacheKey);
            return cachedResult!;
        }

        _logger.LogDebug("Cache miss for currency list: {CacheKey}", cacheKey);

        var query = _context.Currencies.AsNoTracking();

        // Apply search filter
        if (!string.IsNullOrWhiteSpace(search))
        {
            search = search.Trim();
            query = query.Where(c => 
                c.ShortName.Contains(search) || 
                c.LongName.Contains(search));
        }

        var totalCount = await query.CountAsync();

        var currencies = await query
            .OrderBy(c => c.ShortName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var result = new PagedResult<CurrencyDto>
        {
            Items = currencies.Select(MapToDto),
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
        
        _cache.Set(cacheKey, result, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(_cacheOptions.SearchCacheDurationMinutes),
            Size = result.Items.Count()
        });

        return result;
    }

    public async Task<CurrencyDto> CreateAsync(CreateCurrencyRequest request)
    {
        var currency = new Currency
        {
            ShortName = request.ShortName.ToUpperInvariant(),
            LongName = request.LongName.Trim(),
            CreatedDate = DateTime.UtcNow,
            ModifiedDate = DateTime.UtcNow
        };

        _context.Currencies.Add(currency);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Created currency: {ShortName} - {LongName}", currency.ShortName, currency.LongName);

        // Clear relevant caches
        ClearSearchCaches();

        return MapToDto(currency);
    }

    public async Task<CurrencyDto?> UpdateAsync(int id, UpdateCurrencyRequest request)
    {
        var currency = await _context.Currencies.FindAsync(id);
        
        if (currency == null)
        {
            return null;
        }

        var oldShortName = currency.ShortName;
        
        currency.ShortName = request.ShortName.ToUpperInvariant();
        currency.LongName = request.LongName.Trim();
        currency.ModifiedDate = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        _logger.LogInformation("Updated currency ID {Id}: {ShortName} - {LongName}", 
            id, currency.ShortName, currency.LongName);

        // Clear relevant caches
        _cache.Remove($"currency_id_{id}");
        _cache.Remove($"currency_code_{oldShortName}");
        _cache.Remove($"currency_code_{currency.ShortName}");
        ClearSearchCaches();

        return MapToDto(currency);
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var currency = await _context.Currencies.FindAsync(id);
        
        if (currency == null)
        {
            return false;
        }

        var shortName = currency.ShortName;
        
        _context.Currencies.Remove(currency);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Deleted currency ID {Id}: {ShortName}", id, shortName);

        // Clear relevant caches
        _cache.Remove($"currency_id_{id}");
        _cache.Remove($"currency_code_{shortName}");
        ClearSearchCaches();

        return true;
    }

    public async Task<IEnumerable<string>> GetCurrencyCodesAsync()
    {
        var cacheKey = "currency_codes_all";
        
        if (_cache.TryGetValue(cacheKey, out IEnumerable<string>? cachedCodes))
        {
            _logger.LogDebug("Cache hit for currency codes");
            return cachedCodes!;
        }

        _logger.LogDebug("Cache miss for currency codes");
        
        var codes = await _context.Currencies
            .AsNoTracking()
            .OrderBy(c => c.ShortName)
            .Select(c => c.ShortName)
            .ToListAsync();

        _cache.Set(cacheKey, codes, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(_cacheOptions.CurrencyCacheDurationMinutes),
            Size = codes.Count
        });

        return codes;
    }

    private static CurrencyDto MapToDto(Currency currency)
    {
        return new CurrencyDto
        {
            Id = currency.Id,
            ShortName = currency.ShortName,
            LongName = currency.LongName,
            CreatedDate = currency.CreatedDate,
            ModifiedDate = currency.ModifiedDate
        };
    }

    private void ClearSearchCaches()
    {
        // Clear search-related caches - this is a simplified approach
        // In production, you might want to use cache tags or a more sophisticated cache invalidation strategy
        _cache.Remove("currency_codes_all");
        
        // Note: In a real implementation, you'd want to clear paginated search results too
        // For simplicity, we're not implementing a full cache tag system here
    }
}

public class CacheOptions
{
    public int CurrencyCacheDurationMinutes { get; set; } = 60;
    public int SearchCacheDurationMinutes { get; set; } = 30;
    public int MaxCacheSize { get; set; } = 1000;
}