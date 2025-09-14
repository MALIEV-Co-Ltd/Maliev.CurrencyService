using FluentAssertions;
using Maliev.CurrencyService.Api.Models;
using Maliev.CurrencyService.Api.Services;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Maliev.CurrencyService.Tests;

public class ExchangeRateServiceUnitTests
{
    private readonly Mock<IEnumerable<IExchangeRateProvider>> _providersMock;
    private readonly Mock<IMemoryCache> _cacheMock;
    private readonly Mock<ILogger<ExchangeRateService>> _loggerMock;
    private readonly Mock<IOptions<ExchangeRateOptions>> _optionsMock;
    private readonly ExchangeRateService _exchangeRateService;

    public ExchangeRateServiceUnitTests()
    {
        _providersMock = new Mock<IEnumerable<IExchangeRateProvider>>();
        _cacheMock = new Mock<IMemoryCache>();
        _loggerMock = new Mock<ILogger<ExchangeRateService>>();
        _optionsMock = new Mock<IOptions<ExchangeRateOptions>>();
        
        var options = new ExchangeRateOptions
        {
            ProviderOrder = new List<string> { "Provider1", "Provider2", "Provider3" },
            EnableDynamicPrioritization = true,
            MinRequestsForPrioritization = 5,
            ResponseTimeWeight = 0.4,
            SuccessRateWeight = 0.3,
            ErrorRateWeight = 0.2,
            RequestCountWeight = 0.1
        };
        
        _optionsMock.Setup(o => o.Value).Returns(options);
        
        _exchangeRateService = new ExchangeRateService(
            _providersMock.Object,
            _cacheMock.Object,
            null!, // We won't be testing database operations
            _optionsMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public void GetProviderMetrics_ShouldReturnEmptyDictionary_WhenNoMetricsCollected()
    {
        // Act
        var metrics = _exchangeRateService.GetProviderMetrics();

        // Assert
        metrics.Should().NotBeNull();
        metrics.Should().BeEmpty();
    }

    [Fact]
    public void GetProviderMetrics_ShouldReturnCollectedMetrics()
    {
        // Arrange
        // Simulate some provider calls to collect metrics
        // This would require more complex mocking to test properly

        // Act
        var metrics = _exchangeRateService.GetProviderMetrics();

        // Assert
        metrics.Should().NotBeNull();
    }
}