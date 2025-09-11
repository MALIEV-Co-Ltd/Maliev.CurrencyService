using FluentAssertions;
using Maliev.CurrencyService.Api.Models;
using Maliev.CurrencyService.Api.Services;
using Maliev.CurrencyService.Data.DbContexts;
using Maliev.CurrencyService.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Maliev.CurrencyService.Tests;

public class CurrencyServiceUnitTests : IDisposable
{
    private readonly CurrencyDbContext _context;
    private readonly IMemoryCache _cache;
    private readonly Mock<ILogger<Api.Services.CurrencyService>> _loggerMock;
    private readonly Api.Services.CurrencyService _currencyService;
    private readonly CacheOptions _cacheOptions;

    public CurrencyServiceUnitTests()
    {
        // Setup in-memory database
        var options = new DbContextOptionsBuilder<CurrencyDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        
        _context = new CurrencyDbContext(options);
        _cache = new MemoryCache(new MemoryCacheOptions { SizeLimit = 100 });
        _loggerMock = new Mock<ILogger<Api.Services.CurrencyService>>();
        _cacheOptions = new CacheOptions
        {
            CurrencyCacheDurationMinutes = 60,
            SearchCacheDurationMinutes = 30,
            MaxCacheSize = 1000
        };
        
        _currencyService = new Api.Services.CurrencyService(_context, _cache, _loggerMock.Object, _cacheOptions);
        
        // Seed test data
        SeedTestData();
    }

    private void SeedTestData()
    {
        var currencies = new List<Currency>
        {
            new Currency { Id = 1, ShortName = "USD", LongName = "US Dollar", CreatedDate = DateTime.UtcNow, ModifiedDate = DateTime.UtcNow },
            new Currency { Id = 2, ShortName = "EUR", LongName = "Euro", CreatedDate = DateTime.UtcNow, ModifiedDate = DateTime.UtcNow },
            new Currency { Id = 3, ShortName = "THB", LongName = "Thai Baht", CreatedDate = DateTime.UtcNow, ModifiedDate = DateTime.UtcNow },
            new Currency { Id = 4, ShortName = "GBP", LongName = "British Pound Sterling", CreatedDate = DateTime.UtcNow, ModifiedDate = DateTime.UtcNow },
            new Currency { Id = 5, ShortName = "JPY", LongName = "Japanese Yen", CreatedDate = DateTime.UtcNow, ModifiedDate = DateTime.UtcNow }
        };

        _context.Currencies.AddRange(currencies);
        _context.SaveChanges();
    }

    public void Dispose()
    {
        _context.Dispose();
        _cache.Dispose();
    }

    [Fact]
    public async Task GetAllAsync_ShouldReturnPagedResult()
    {
        // Act
        var result = await _currencyService.GetAllAsync();

        // Assert
        result.Should().NotBeNull();
        result.Items.Should().HaveCount(5);
        result.TotalCount.Should().Be(5);
        result.Page.Should().Be(1);
        result.PageSize.Should().Be(20);
        result.Items.Should().Contain(c => c.ShortName == "USD");
        result.Items.Should().Contain(c => c.ShortName == "EUR");
        result.Items.Should().Contain(c => c.ShortName == "THB");
    }

    [Fact]
    public async Task GetAllAsync_WithPagination_ShouldReturnCorrectPage()
    {
        // Act
        var result = await _currencyService.GetAllAsync(page: 1, pageSize: 2);

        // Assert
        result.Should().NotBeNull();
        result.Items.Should().HaveCount(2);
        result.TotalCount.Should().Be(5);
        result.Page.Should().Be(1);
        result.PageSize.Should().Be(2);
    }

    [Fact]
    public async Task GetAllAsync_WithSearch_ShouldReturnFilteredResults()
    {
        // Act
        var result = await _currencyService.GetAllAsync(search: "Dollar");

        // Assert
        result.Should().NotBeNull();
        result.Items.Should().HaveCount(1);
        result.Items.First().ShortName.Should().Be("USD");
    }

    [Fact]
    public async Task GetAllAsync_ShouldCacheResults()
    {
        // Act - First call
        var result1 = await _currencyService.GetAllAsync();
        
        // Act - Second call
        var result2 = await _currencyService.GetAllAsync();

        // Assert
        result1.Should().BeEquivalentTo(result2);
        
        // Verify cache was used
        var cacheKey = "currency_list_1_20_all";
        _cache.TryGetValue(cacheKey, out var cachedValue).Should().BeTrue();
        cachedValue.Should().NotBeNull();
    }

    [Fact]
    public async Task GetByIdAsync_WithValidId_ShouldReturnCurrency()
    {
        // Act
        var result = await _currencyService.GetByIdAsync(1);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(1);
        result.ShortName.Should().Be("USD");
        result.LongName.Should().Be("US Dollar");
    }

    [Fact]
    public async Task GetByIdAsync_WithInvalidId_ShouldReturnNull()
    {
        // Act
        var result = await _currencyService.GetByIdAsync(999);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByIdAsync_ShouldCacheResults()
    {
        // Act - First call
        var result1 = await _currencyService.GetByIdAsync(1);
        
        // Act - Second call
        var result2 = await _currencyService.GetByIdAsync(1);

        // Assert
        result1.Should().BeEquivalentTo(result2);
        
        // Verify cache was used
        var cacheKey = "currency_id_1";
        _cache.TryGetValue(cacheKey, out var cachedValue).Should().BeTrue();
        cachedValue.Should().NotBeNull();
    }

    [Fact]
    public async Task GetByShortNameAsync_WithValidShortName_ShouldReturnCurrency()
    {
        // Act
        var result = await _currencyService.GetByShortNameAsync("USD");

        // Assert
        result.Should().NotBeNull();
        result!.ShortName.Should().Be("USD");
        result.LongName.Should().Be("US Dollar");
    }

    [Fact]
    public async Task GetByShortNameAsync_WithInvalidShortName_ShouldReturnNull()
    {
        // Act
        var result = await _currencyService.GetByShortNameAsync("XYZ");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByShortNameAsync_ShouldBeCaseInsensitive()
    {
        // Act
        var result1 = await _currencyService.GetByShortNameAsync("usd");
        var result2 = await _currencyService.GetByShortNameAsync("USD");
        var result3 = await _currencyService.GetByShortNameAsync("Usd");

        // Assert
        result1.Should().NotBeNull();
        result2.Should().NotBeNull();
        result3.Should().NotBeNull();
        result1!.ShortName.Should().Be("USD");
        result2!.ShortName.Should().Be("USD");
        result3!.ShortName.Should().Be("USD");
    }

    [Fact]
    public async Task CreateAsync_WithValidData_ShouldCreateCurrency()
    {
        // Arrange
        var createRequest = new CreateCurrencyRequest
        {
            ShortName = "CAD",
            LongName = "Canadian Dollar"
        };

        // Act
        var result = await _currencyService.CreateAsync(createRequest);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().BeGreaterThan(0);
        result.ShortName.Should().Be("CAD");
        result.LongName.Should().Be("Canadian Dollar");

        // Verify it's in the database
        var createdCurrency = await _context.Currencies.FindAsync(result.Id);
        createdCurrency.Should().NotBeNull();
        createdCurrency!.ShortName.Should().Be("CAD");
    }

    [Fact]
    public async Task CreateAsync_ShouldNormalizeShortName()
    {
        // Arrange
        var createRequest = new CreateCurrencyRequest
        {
            ShortName = "cad", // lowercase
            LongName = "Canadian Dollar"
        };

        // Act
        var result = await _currencyService.CreateAsync(createRequest);

        // Assert
        result.ShortName.Should().Be("CAD"); // Should be uppercase
    }

    [Fact]
    public async Task UpdateAsync_WithValidData_ShouldUpdateCurrency()
    {
        // Arrange
        var updateRequest = new UpdateCurrencyRequest
        {
            ShortName = "USD",
            LongName = "Updated US Dollar"
        };

        // Act
        var result = await _currencyService.UpdateAsync(1, updateRequest);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(1);
        result.ShortName.Should().Be("USD");
        result.LongName.Should().Be("Updated US Dollar");

        // Verify in database
        var updatedCurrency = await _context.Currencies.FindAsync(1);
        updatedCurrency!.LongName.Should().Be("Updated US Dollar");
    }

    [Fact]
    public async Task UpdateAsync_WithInvalidId_ShouldReturnNull()
    {
        // Arrange
        var updateRequest = new UpdateCurrencyRequest
        {
            ShortName = "XYZ",
            LongName = "Non-existent Currency"
        };

        // Act
        var result = await _currencyService.UpdateAsync(999, updateRequest);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task DeleteAsync_WithValidId_ShouldDeleteCurrency()
    {
        // Act
        var result = await _currencyService.DeleteAsync(1);

        // Assert
        result.Should().BeTrue();

        // Verify it's deleted
        var deletedCurrency = await _context.Currencies.FindAsync(1);
        deletedCurrency.Should().BeNull();
    }

    [Fact]
    public async Task DeleteAsync_WithInvalidId_ShouldReturnFalse()
    {
        // Act
        var result = await _currencyService.DeleteAsync(999);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task GetCurrencyCodesAsync_ShouldReturnAllCodes()
    {
        // Act
        var result = await _currencyService.GetCurrencyCodesAsync();

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(5);
        result.Should().Contain("USD");
        result.Should().Contain("EUR");
        result.Should().Contain("THB");
        result.Should().Contain("GBP");
        result.Should().Contain("JPY");
    }

    [Fact]
    public async Task GetCurrencyCodesAsync_ShouldCacheResults()
    {
        // Act - First call
        var result1 = await _currencyService.GetCurrencyCodesAsync();
        
        // Act - Second call
        var result2 = await _currencyService.GetCurrencyCodesAsync();

        // Assert
        result1.Should().BeEquivalentTo(result2);
        
        // Verify cache was used
        var cacheKey = "currency_codes_all";
        _cache.TryGetValue(cacheKey, out var cachedValue).Should().BeTrue();
        cachedValue.Should().NotBeNull();
    }
}