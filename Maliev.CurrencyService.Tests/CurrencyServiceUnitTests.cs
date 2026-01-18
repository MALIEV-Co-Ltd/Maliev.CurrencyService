using Maliev.Aspire.ServiceDefaults.Caching;
using Maliev.CurrencyService.Api.Models.Currencies;
using Maliev.CurrencyService.Api.Services;
using Maliev.CurrencyService.Data;
using Maliev.CurrencyService.Data.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Maliev.CurrencyService.Tests;

public class CurrencyServiceUnitTests : IDisposable
{
    private readonly CurrencyDbContext _context;
    private readonly Mock<ICacheService> _cacheMock;
    private readonly Mock<ILogger<Api.Services.CurrencyService>> _loggerMock;
    private readonly Api.Services.CurrencyService _currencyService;

    public CurrencyServiceUnitTests()
    {
        var options = new DbContextOptionsBuilder<CurrencyDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _context = new CurrencyDbContext(options);
        _cacheMock = new Mock<ICacheService>();
        _loggerMock = new Mock<ILogger<Api.Services.CurrencyService>>();
        _currencyService = new Api.Services.CurrencyService(_context, _cacheMock.Object, _loggerMock.Object);
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    [Fact]
    public async Task GetByCodeAsync_Should_Return_Currency_When_Exists()
    {
        // Arrange
        var currency = new Currency { Code = "USD", Name = "US Dollar", Symbol = "$", DecimalPlaces = 2, IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        _context.Currencies.Add(currency);
        await _context.SaveChangesAsync();

        // Act
        var result = await _currencyService.GetByCodeAsync("USD");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("USD", result!.Code);
    }

    [Fact]
    public async Task GetByCodeAsync_Should_Return_Null_When_Not_Exists()
    {
        // Act
        var result = await _currencyService.GetByCodeAsync("XYZ");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetByCountryCodeAsync_Should_Return_Currency_For_Iso2()
    {
        // Arrange
        var currency = new Currency { Code = "THB", Name = "Thai Baht", Symbol = "฿", DecimalPlaces = 2, IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        var mapping = new CountryCurrency { CountryIso2 = "TH", CountryIso3 = "THA", CurrencyCode = "THB", IsPrimary = true };
        _context.Currencies.Add(currency);
        _context.CountryCurrencies.Add(mapping);
        await _context.SaveChangesAsync();

        // Act
        var result = await _currencyService.GetByCountryCodeAsync("TH");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("THB", result!.Code);
    }

    [Fact]
    public async Task CreateAsync_Should_Throw_If_Already_Exists()
    {
        // Arrange
        var currency = new Currency { Code = "USD", Name = "US Dollar", Symbol = "$", DecimalPlaces = 2, IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        _context.Currencies.Add(currency);
        await _context.SaveChangesAsync();

        var request = new Maliev.CurrencyService.Api.Models.Currencies.CreateCurrencyRequest { Code = "USD", Name = "New Dollar", Symbol = "$", DecimalPlaces = 2 };

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => _currencyService.CreateAsync(request));
    }

    [Fact]
    public async Task ActivateAsync_Should_Set_IsActive_True()
    {
        // Arrange
        var id = Guid.NewGuid();
        var currency = new Currency { Id = id, Code = "GBP", Name = "Pound", Symbol = "£", DecimalPlaces = 2, IsActive = false, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        _context.Currencies.Add(currency);
        await _context.SaveChangesAsync();

        // Act
        var result = await _currencyService.ActivateAsync(id);

        // Assert
        Assert.True(result);
        var updated = await _context.Currencies.FindAsync(id);
        Assert.True(updated!.IsActive);
    }

    [Fact]
    public async Task DeactivateAsync_Should_Set_IsActive_False()
    {
        // Arrange
        var id = Guid.NewGuid();
        var currency = new Currency { Id = id, Code = "GBP", Name = "Pound", Symbol = "£", DecimalPlaces = 2, IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        _context.Currencies.Add(currency);
        await _context.SaveChangesAsync();

        // Act
        var result = await _currencyService.DeactivateAsync(id);

        // Assert
        Assert.True(result);
        var updated = await _context.Currencies.FindAsync(id);
        Assert.False(updated!.IsActive);
    }
}
