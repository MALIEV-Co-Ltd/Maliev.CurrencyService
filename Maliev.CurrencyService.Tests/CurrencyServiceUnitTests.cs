using Maliev.Aspire.ServiceDefaults.Caching;
using Maliev.CurrencyService.Application.DTOs.Currencies;
using Maliev.CurrencyService.Application.Interfaces;
using Maliev.CurrencyService.Domain.Entities;
using Maliev.CurrencyService.Infrastructure.Persistence;
using CurrencyServiceImpl = Maliev.CurrencyService.Infrastructure.Services.CurrencyService;
using Maliev.CurrencyService.Tests.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Maliev.CurrencyService.Tests;

/// <summary>
/// Unit tests for <see cref="CurrencyService"/> using real PostgreSQL via Testcontainers.
/// </summary>
public class CurrencyServiceUnitTests : IClassFixture<BaseIntegrationTestFactory<Program, CurrencyDbContext>>, IAsyncLifetime
{
    private readonly BaseIntegrationTestFactory<Program, CurrencyDbContext> _factory;
    private CurrencyDbContext _context = null!;

    /// <summary>Initializes a new instance of the <see cref="CurrencyServiceUnitTests"/> class.</summary>
    public CurrencyServiceUnitTests(BaseIntegrationTestFactory<Program, CurrencyDbContext> factory)
    {
        _factory = factory;
    }

    /// <inheritdoc/>
    public async Task InitializeAsync()
    {
        await _factory.CleanDatabaseAsync();
        _context = _factory.CreateDbContext();
    }

    /// <inheritdoc/>
    public async Task DisposeAsync()
    {
        await _context.DisposeAsync();
    }

    private CurrencyServiceImpl CreateService()
    {
        var cacheMock = new Mock<ICacheService>();
        var loggerMock = new Mock<ILogger<CurrencyServiceImpl>>();
        return new CurrencyServiceImpl(_context, cacheMock.Object, loggerMock.Object);
    }

    /// <summary>GetByCodeAsync returns the currency when it exists.</summary>
    [Fact]
    public async Task GetByCodeAsync_Should_Return_Currency_When_Exists()
    {
        // Arrange
        var currency = new Currency { Code = "USD", Name = "US Dollar", Symbol = "$", DecimalPlaces = 2, IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        _context.Currencies.Add(currency);
        await _context.SaveChangesAsync();

        var service = CreateService();

        // Act
        var result = await service.GetByCodeAsync("USD");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("USD", result!.Code);
    }

    /// <summary>GetByCodeAsync returns null when currency does not exist.</summary>
    [Fact]
    public async Task GetByCodeAsync_Should_Return_Null_When_Not_Exists()
    {
        var service = CreateService();

        // Act
        var result = await service.GetByCodeAsync("XYZ");

        // Assert
        Assert.Null(result);
    }

    /// <summary>GetByCountryCodeAsync returns the currency for a given ISO2 country code.</summary>
    [Fact]
    public async Task GetByCountryCodeAsync_Should_Return_Currency_For_Iso2()
    {
        // Arrange
        var currency = new Currency { Code = "THB", Name = "Thai Baht", Symbol = "฿", DecimalPlaces = 2, IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        var mapping = new CountryCurrency { CountryIso2 = "TH", CountryIso3 = "THA", CurrencyCode = "THB", IsPrimary = true };
        _context.Currencies.Add(currency);
        _context.CountryCurrencies.Add(mapping);
        await _context.SaveChangesAsync();

        var service = CreateService();

        // Act
        var result = await service.GetByCountryCodeAsync("TH");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("THB", result!.Code);
    }

    /// <summary>CreateAsync throws when currency already exists.</summary>
    [Fact]
    public async Task CreateAsync_Should_Throw_If_Already_Exists()
    {
        // Arrange
        var currency = new Currency { Code = "USD", Name = "US Dollar", Symbol = "$", DecimalPlaces = 2, IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        _context.Currencies.Add(currency);
        await _context.SaveChangesAsync();

        var request = new Application.DTOs.Currencies.CreateCurrencyRequest { Code = "USD", Name = "New Dollar", Symbol = "$", DecimalPlaces = 2 };
        var service = CreateService();

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(request));
    }

    /// <summary>ActivateAsync sets IsActive to true.</summary>
    [Fact]
    public async Task ActivateAsync_Should_Set_IsActive_True()
    {
        // Arrange
        var id = Guid.NewGuid();
        var currency = new Currency { Id = id, Code = "GBP", Name = "Pound", Symbol = "£", DecimalPlaces = 2, IsActive = false, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        _context.Currencies.Add(currency);
        await _context.SaveChangesAsync();

        var service = CreateService();

        // Act
        var result = await service.ActivateAsync(id);

        // Assert
        Assert.True(result);
        var updated = await _context.Currencies.FindAsync(id);
        Assert.True(updated!.IsActive);
    }

    /// <summary>DeactivateAsync sets IsActive to false.</summary>
    [Fact]
    public async Task DeactivateAsync_Should_Set_IsActive_False()
    {
        // Arrange
        var id = Guid.NewGuid();
        var currency = new Currency { Id = id, Code = "GBP", Name = "Pound", Symbol = "£", DecimalPlaces = 2, IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        _context.Currencies.Add(currency);
        await _context.SaveChangesAsync();

        var service = CreateService();

        // Act
        var result = await service.DeactivateAsync(id);

        // Assert
        Assert.True(result);
        var updated = await _context.Currencies.FindAsync(id);
        Assert.False(updated!.IsActive);
    }
}
