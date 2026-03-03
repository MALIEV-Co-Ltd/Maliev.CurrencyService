using Maliev.Aspire.ServiceDefaults.Caching;
using Maliev.CurrencyService.Application.DTOs.Currencies;
using Maliev.CurrencyService.Application.DTOs.Rates;
using Maliev.CurrencyService.Application.DTOs.Snapshots;
using Maliev.CurrencyService.Application.Interfaces;
using Maliev.CurrencyService.Domain.Entities;
using Maliev.CurrencyService.Infrastructure.Data.SeedData;
using Maliev.CurrencyService.Infrastructure.Persistence;
using Maliev.CurrencyService.Infrastructure.Services;
using Maliev.CurrencyService.Tests.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using CurrencyServiceImpl = Maliev.CurrencyService.Infrastructure.Services.CurrencyService;
using AppCreateCurrencyRequest = Maliev.CurrencyService.Application.DTOs.Currencies.CreateCurrencyRequest;
using AppUpdateCurrencyRequest = Maliev.CurrencyService.Application.DTOs.Currencies.UpdateCurrencyRequest;

namespace Maliev.CurrencyService.Tests;

public class CurrencyServiceIntegrationTests : IClassFixture<BaseIntegrationTestFactory<Program, CurrencyDbContext>>, IAsyncLifetime
{
    private readonly BaseIntegrationTestFactory<Program, CurrencyDbContext> _factory;
    private CurrencyDbContext _context = null!;
    private readonly Mock<ILogger<CurrencyServiceImpl>> _loggerMock;
    private readonly Mock<ICacheService> _cacheServiceMock;

    public CurrencyServiceIntegrationTests(BaseIntegrationTestFactory<Program, CurrencyDbContext> factory)
    {
        _factory = factory;
        _loggerMock = new Mock<ILogger<CurrencyServiceImpl>>();
        _cacheServiceMock = new Mock<ICacheService>();
        _cacheServiceMock.Setup(c => c.GetAsync<PaginatedCurrencyResponse>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PaginatedCurrencyResponse?)null);
        _cacheServiceMock.Setup(c => c.GetAsync<CurrencyResponse>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CurrencyResponse?)null);
    }

    public async Task InitializeAsync()
    {
        await _factory.CleanDatabaseAsync();
        _context = _factory.CreateDbContext();
    }

    public async Task DisposeAsync()
    {
        await _context.DisposeAsync();
    }

    private CurrencyServiceImpl CreateService() => new CurrencyServiceImpl(_context, _cacheServiceMock.Object, _loggerMock.Object);

    [Fact]
    public async Task GetAllAsync_ReturnsCurrencies_WhenDataExists()
    {
        var service = CreateService();

        var usd = new Currency
        {
            Code = "USD",
            Symbol = "$",
            Name = "US Dollar",
            DecimalPlaces = 2,
            IsActive = true,
            IsPrimary = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Version = new byte[8]
        };
        _context.Currencies.Add(usd);
        await _context.SaveChangesAsync();

        var result = await service.GetAllAsync(1, 50, null, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Single(result.Items);
        Assert.Equal("USD", result.Items.First().Code);
    }

    [Fact]
    public async Task GetAllAsync_FiltersByIsActive()
    {
        var service = CreateService();

        _context.Currencies.AddRange(
            new Currency { Code = "USD", Symbol = "$", Name = "US Dollar", DecimalPlaces = 2, IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow, Version = new byte[8] },
            new Currency { Code = "EUR", Symbol = "€", Name = "Euro", DecimalPlaces = 2, IsActive = false, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow, Version = new byte[8] }
        );
        await _context.SaveChangesAsync();

        var result = await service.GetAllAsync(1, 50, true, CancellationToken.None);

        Assert.Single(result.Items);
        Assert.Equal("USD", result.Items.First().Code);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsCurrency_WhenExists()
    {
        var service = CreateService();

        var currency = new Currency
        {
            Code = "THB",
            Symbol = "฿",
            Name = "Thai Baht",
            DecimalPlaces = 2,
            IsActive = true,
            IsPrimary = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Version = new byte[8]
        };
        _context.Currencies.Add(currency);
        await _context.SaveChangesAsync();

        var result = await service.GetByIdAsync(currency.Id, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("THB", result.Code);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsNull_WhenNotExists()
    {
        var service = CreateService();

        var result = await service.GetByIdAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetByCodeAsync_ReturnsCurrency_WhenExists()
    {
        var service = CreateService();

        var currency = new Currency
        {
            Code = "GBP",
            Symbol = "£",
            Name = "British Pound",
            DecimalPlaces = 2,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Version = new byte[8]
        };
        _context.Currencies.Add(currency);
        await _context.SaveChangesAsync();

        var result = await service.GetByCodeAsync("GBP", CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("£", result.Symbol);
    }

    [Fact]
    public async Task CreateAsync_AddsCurrency()
    {
        var service = CreateService();

        var request = new AppCreateCurrencyRequest
        {
            Code = "JPY",
            Symbol = "¥",
            Name = "Japanese Yen",
            DecimalPlaces = 0
        };

        var result = await service.CreateAsync(request, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("JPY", result.Code);
        Assert.Equal("¥", result.Symbol);
    }

    [Fact]
    public async Task UpdateAsync_ModifiesCurrency()
    {
        var service = CreateService();

        var currency = new Currency
        {
            Code = "CAD",
            Symbol = "$",
            Name = "Canadian Dollar",
            DecimalPlaces = 2,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Version = new byte[8]
        };
        _context.Currencies.Add(currency);
        await _context.SaveChangesAsync();

        var request = new AppUpdateCurrencyRequest
        {
            Name = "Canadian Dollar - Updated",
            Symbol = "C$",
            DecimalPlaces = 2
        };

        var result = await service.UpdateAsync("CAD", request, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("Canadian Dollar - Updated", result.Name);
    }

    [Fact]
    public async Task DeleteAsync_RemovesCurrency()
    {
        var service = CreateService();

        var currency = new Currency
        {
            Code = "AUD",
            Symbol = "$",
            Name = "Australian Dollar",
            DecimalPlaces = 2,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Version = new byte[8]
        };
        _context.Currencies.Add(currency);
        await _context.SaveChangesAsync();

        var deleted = await service.DeleteAsync("AUD", CancellationToken.None);

        Assert.True(deleted);
    }

    [Fact]
    public async Task GetByCountryCodeAsync_ReturnsCurrency_WhenMappingExists()
    {
        var service = CreateService();

        var currency = new Currency
        {
            Code = "USD",
            Symbol = "$",
            Name = "US Dollar",
            DecimalPlaces = 2,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Version = new byte[8]
        };
        var countryMapping = new CountryCurrency
        {
            CountryIso2 = "US",
            CountryIso3 = "USA",
            CurrencyCode = "USD",
            IsPrimary = true,
            CreatedAt = DateTime.UtcNow
        };
        _context.Currencies.Add(currency);
        _context.CountryCurrencies.Add(countryMapping);
        await _context.SaveChangesAsync();

        var result = await service.GetByCountryCodeAsync("US", CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("USD", result.Code);
    }

    [Fact]
    public async Task ActivateAsync_SetsIsActiveToTrue()
    {
        var service = CreateService();

        var currency = new Currency
        {
            Code = "NZD",
            Symbol = "$",
            Name = "New Zealand Dollar",
            DecimalPlaces = 2,
            IsActive = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Version = new byte[8]
        };
        _context.Currencies.Add(currency);
        await _context.SaveChangesAsync();

        var result = await service.ActivateAsync(currency.Id, CancellationToken.None);

        Assert.True(result);
        await _context.Entry(currency).ReloadAsync();
        Assert.True(currency.IsActive);
    }

    [Fact]
    public async Task DeactivateAsync_SetsIsActiveToFalse()
    {
        var service = CreateService();

        var currency = new Currency
        {
            Code = "SGD",
            Symbol = "$",
            Name = "Singapore Dollar",
            DecimalPlaces = 2,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Version = new byte[8]
        };
        _context.Currencies.Add(currency);
        await _context.SaveChangesAsync();

        var result = await service.DeactivateAsync(currency.Id, CancellationToken.None);

        Assert.True(result);
        await _context.Entry(currency).ReloadAsync();
        Assert.False(currency.IsActive);
    }
}

public class SeedDataTests
{
    [Fact]
    public void GetAll_ReturnsAllCurrencies()
    {
        var currencies = CurrencySeedData.GetAll();

        Assert.NotNull(currencies);
        Assert.NotEmpty(currencies);
    }

    [Fact]
    public void GetAll_ContainsUSD()
    {
        var currencies = CurrencySeedData.GetAll().ToList();

        var usd = currencies.FirstOrDefault(c => c.Code == "USD");
        Assert.NotNull(usd);
        Assert.Equal("$", usd.Symbol);
    }

    [Fact]
    public void GetAll_ContainsTHB()
    {
        var currencies = CurrencySeedData.GetAll().ToList();

        var thb = currencies.FirstOrDefault(c => c.Code == "THB");
        Assert.NotNull(thb);
        Assert.True(thb.IsPrimary);
    }

    [Fact]
    public void GetAll_AllCurrenciesHaveValidCodes()
    {
        var currencies = CurrencySeedData.GetAll().ToList();

        foreach (var currency in currencies)
        {
            Assert.NotNull(currency.Code);
            Assert.Equal(3, currency.Code.Length);
        }
    }

    [Fact]
    public void GetAll_AllCurrenciesHaveValidDecimalPlaces()
    {
        var currencies = CurrencySeedData.GetAll().ToList();

        foreach (var currency in currencies)
        {
            Assert.InRange(currency.DecimalPlaces, 0, 8);
        }
    }
}

public class DomainEntityTests
{
    [Fact]
    public void Currency_Properties_CanBeSet()
    {
        var currency = new Currency
        {
            Id = Guid.NewGuid(),
            Code = "EUR",
            Symbol = "€",
            Name = "Euro",
            DecimalPlaces = 2,
            IsActive = true,
            IsPrimary = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Version = new byte[8]
        };

        Assert.Equal("EUR", currency.Code);
        Assert.Equal("€", currency.Symbol);
        Assert.Equal("Euro", currency.Name);
        Assert.Equal(2, currency.DecimalPlaces);
        Assert.True(currency.IsActive);
    }

    [Fact]
    public void ExchangeRate_Properties_CanBeSet()
    {
        var rate = new ExchangeRate
        {
            Id = Guid.NewGuid(),
            FromCurrency = "USD",
            ToCurrency = "EUR",
            Rate = 0.85m,
            Provider = "Fawazahmed",
            IsTransitive = false,
            CreatedAt = DateTime.UtcNow
        };

        Assert.Equal("USD", rate.FromCurrency);
        Assert.Equal("EUR", rate.ToCurrency);
        Assert.Equal(0.85m, rate.Rate);
        Assert.Equal("Fawazahmed", rate.Provider);
    }

    [Fact]
    public void RateSnapshot_Properties_CanBeSet()
    {
        var snapshot = new RateSnapshot
        {
            Id = Guid.NewGuid(),
            SnapshotDate = DateOnly.FromDateTime(DateTime.UtcNow),
            FromCurrency = "USD",
            ToCurrency = "EUR",
            Rate = 0.85m,
            Source = "Frankfurter",
            CreatedAt = DateTime.UtcNow
        };

        Assert.Equal("USD", snapshot.FromCurrency);
        Assert.Equal("EUR", snapshot.ToCurrency);
        Assert.Equal(0.85m, snapshot.Rate);
        Assert.Equal("Frankfurter", snapshot.Source);
    }

    [Fact]
    public void CountryCurrency_Properties_CanBeSet()
    {
        var mapping = new CountryCurrency
        {
            Id = Guid.NewGuid(),
            CountryIso2 = "TH",
            CountryIso3 = "THA",
            CurrencyCode = "THB",
            IsPrimary = true,
            CreatedAt = DateTime.UtcNow
        };

        Assert.Equal("TH", mapping.CountryIso2);
        Assert.Equal("THA", mapping.CountryIso3);
        Assert.Equal("THB", mapping.CurrencyCode);
        Assert.True(mapping.IsPrimary);
    }

    [Fact]
    public void BatchStatus_Properties_CanBeSet()
    {
        var batchStatus = new BatchStatus
        {
            Id = Guid.NewGuid(),
            BatchId = Guid.NewGuid().ToString(),
            Status = "Processing",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        Assert.Equal("Processing", batchStatus.Status);
    }
}

public class DtoTests
{
    [Fact]
    public void CreateCurrencyRequest_Properties_CanBeSet()
    {
        var request = new AppCreateCurrencyRequest
        {
            Code = "EUR",
            Symbol = "€",
            Name = "Euro",
            DecimalPlaces = 2
        };

        Assert.Equal("EUR", request.Code);
        Assert.Equal("€", request.Symbol);
        Assert.Equal("Euro", request.Name);
        Assert.Equal(2, request.DecimalPlaces);
    }

    [Fact]
    public void UpdateCurrencyRequest_Properties_CanBeSet()
    {
        var request = new AppUpdateCurrencyRequest
        {
            Name = "Updated Name",
            Symbol = "£",
            DecimalPlaces = 2,
            IsActive = true
        };

        Assert.Equal("Updated Name", request.Name);
        Assert.Equal("£", request.Symbol);
        Assert.Equal(2, request.DecimalPlaces);
        Assert.True(request.IsActive);
    }

    [Fact]
    public void UpdateCurrencyRequest_OptionalProperties_CanBeNull()
    {
        var request = new AppUpdateCurrencyRequest
        {
            Name = "New Name"
        };

        Assert.Null(request.Symbol);
        Assert.Null(request.DecimalPlaces);
        Assert.Null(request.IsActive);
    }

    [Fact]
    public void UpdateCurrencyRequest_WithVersion_CanBeSet()
    {
        var version = new byte[] { 1, 2, 3 };
        var request = new AppUpdateCurrencyRequest
        {
            Name = "Updated",
            Version = version
        };

        Assert.NotNull(request.Version);
        Assert.Equal(3, request.Version.Length);
    }

    [Fact]
    public void ExchangeRateResponse_LiveMode_Properties_CanBeSet()
    {
        var now = DateTime.UtcNow;
        var response = new ExchangeRateResponse
        {
            FromCurrency = "USD",
            ToCurrency = "EUR",
            Rate = 0.85m,
            Timestamp = now,
            Source = "Fawazahmed",
            IsTransitive = false,
            Mode = "live"
        };

        Assert.Equal("USD", response.FromCurrency);
        Assert.Equal("EUR", response.ToCurrency);
        Assert.Equal(0.85m, response.Rate);
        Assert.Equal("Fawazahmed", response.Source);
        Assert.Equal("live", response.Mode);
        Assert.False(response.IsTransitive);
    }

    [Fact]
    public void ExchangeRateResponse_TransitiveMode_IncludesIntermediateCurrency()
    {
        var response = new ExchangeRateResponse
        {
            FromCurrency = "THB",
            ToCurrency = "GBP",
            Rate = 0.023m,
            Timestamp = DateTime.UtcNow,
            Source = "Transitive:Fawazahmed,Frankfurter",
            IsTransitive = true,
            IntermediateCurrency = "USD",
            CalculationDetails = "USD/THB × THB/GBP",
            Mode = "live"
        };

        Assert.True(response.IsTransitive);
        Assert.Equal("USD", response.IntermediateCurrency);
        Assert.Equal("USD/THB × THB/GBP", response.CalculationDetails);
    }

    [Fact]
    public void ExchangeRateResponse_SnapshotMode_IncludesSnapshotDate()
    {
        var snapshotDate = DateOnly.FromDateTime(DateTime.UtcNow);
        var response = new ExchangeRateResponse
        {
            FromCurrency = "USD",
            ToCurrency = "EUR",
            Rate = 0.85m,
            Timestamp = DateTime.UtcNow,
            Source = "Snapshot",
            IsTransitive = false,
            Mode = "snapshot",
            SnapshotDate = snapshotDate
        };

        Assert.Equal("snapshot", response.Mode);
        Assert.Equal(snapshotDate, response.SnapshotDate);
    }

    [Fact]
    public void SnapshotBatchRequest_Properties_CanBeSet()
    {
        var request = new SnapshotBatchRequest
        {
            SnapshotDate = DateOnly.FromDateTime(DateTime.UtcNow),
            Source = "TestProvider",
            AutoPromote = true,
            Snapshots = new List<SnapshotEntry>
            {
                new() { From = "USD", To = "EUR", Rate = 0.85m },
                new() { From = "USD", To = "GBP", Rate = 0.75m }
            }
        };

        Assert.NotNull(request.Snapshots);
        Assert.Equal(2, request.Snapshots.Count);
        Assert.True(request.AutoPromote);
        Assert.Equal("TestProvider", request.Source);
    }

    [Fact]
    public void SnapshotEntry_Properties_CanBeSet()
    {
        var entry = new SnapshotEntry
        {
            From = "USD",
            To = "EUR",
            Rate = 0.85m
        };

        Assert.Equal("USD", entry.From);
        Assert.Equal("EUR", entry.To);
        Assert.Equal(0.85m, entry.Rate);
    }
}

public class ErrorResponseTests
{
    [Fact]
    public void ErrorResponse_Properties_CanBeSet()
    {
        var timestamp = DateTime.UtcNow;
        var error = new Api.Models.Common.ErrorResponse
        {
            Error = "ValidationError",
            Message = "Invalid input",
            Timestamp = timestamp,
            CorrelationId = "corr-123"
        };

        Assert.Equal("ValidationError", error.Error);
        Assert.Equal("Invalid input", error.Message);
        Assert.Equal(timestamp, error.Timestamp);
        Assert.Equal("corr-123", error.CorrelationId);
    }

    [Fact]
    public void ErrorResponse_WithDetails_CanBeSet()
    {
        var error = new Api.Models.Common.ErrorResponse
        {
            Error = "BadRequest",
            Message = "Validation failed",
            Timestamp = DateTime.UtcNow,
            Details = new Dictionary<string, string[]>
            {
                { "Code", new[] { "Required" } }
            }
        };

        Assert.NotNull(error.Details);
        Assert.Contains("Code", error.Details);
    }
}

public class ApiModelTests
{
    [Fact]
    public void PagedResult_Properties_CanBeSet()
    {
        var result = new Api.Models.PagedResult<string>
        {
            Items = new List<string> { "item1", "item2" },
            TotalCount = 2,
            Page = 1,
            PageSize = 10
        };

        Assert.Equal(2, ((List<string>)result.Items).Count);
        Assert.Equal(2, result.TotalCount);
        Assert.Equal(1, result.Page);
    }

    [Fact]
    public void PagedResult_CalculatesTotalPages_Correctly()
    {
        var result = new Api.Models.PagedResult<string>
        {
            Items = new List<string> { "item1" },
            TotalCount = 100,
            Page = 1,
            PageSize = 1
        };

        Assert.Equal(100, result.TotalPages);
    }

    [Fact]
    public void PaginatedResponse_HasCorrectPageProperties()
    {
        var response = new Api.Models.Common.PaginatedResponse<string>
        {
            Items = new List<string> { "item1", "item2" },
            Page = 1,
            PageSize = 10,
            TotalCount = 2,
            TotalPages = 1
        };

        Assert.False(response.HasNextPage);
        Assert.False(response.HasPreviousPage);
    }

    [Fact]
    public void OpenRatesModel_Properties_CanBeSet()
    {
        var model = new Api.Models.ApiResponses.OpenRatesModel
        {
            Base = "USD",
            Date = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd"),
            Rates = new Dictionary<string, decimal>
            {
                { "EUR", 0.85m },
                { "GBP", 0.75m }
            }
        };

        Assert.Equal("USD", model.Base);
        Assert.NotNull(model.Rates);
        Assert.Equal(2, model.Rates.Count);
    }

    [Fact]
    public void OpenRatesModel_WithEmptyRates_Works()
    {
        var model = new Api.Models.ApiResponses.OpenRatesModel
        {
            Base = "USD",
            Date = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd"),
            Rates = new Dictionary<string, decimal>()
        };

        Assert.NotNull(model.Rates);
        Assert.Empty(model.Rates);
    }
}
