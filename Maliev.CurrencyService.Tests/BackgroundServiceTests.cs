using Maliev.CurrencyService.Api.BackgroundServices;
using Maliev.CurrencyService.Api.Metrics;
using Maliev.CurrencyService.Api.Models.Rates;
using Maliev.CurrencyService.Api.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Maliev.CurrencyService.Tests;

public class BackgroundServiceTests
{
    [Fact]
    public async Task CacheWarmingService_Executes_WarmsTopPairs()
    {
        // Arrange
        var services = new ServiceCollection();
        var rateServiceMock = new Mock<IRateService>();

        // Setup mock to return a rate for any pair
        rateServiceMock.Setup(s => s.GetLiveRateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ExchangeRateResponse
            {
                Rate = 1.0m,
                FromCurrency = "USD",
                ToCurrency = "EUR",
                Timestamp = DateTime.UtcNow,
                Source = "Test",
                IsTransitive = false,
                Mode = "live"
            });

        services.AddSingleton(rateServiceMock.Object);
        var serviceProvider = services.BuildServiceProvider();

        var loggerMock = new Mock<ILogger<CacheWarmingService>>();
        var configMock = new Mock<IConfiguration>();
        configMock.Setup(c => c["ASPNETCORE_ENVIRONMENT"]).Returns("Test");
        var metrics = new CurrencyServiceMetrics(configMock.Object);

        var service = new CacheWarmingService(serviceProvider, loggerMock.Object, metrics);

        // Act
        var cts = new CancellationTokenSource();
        // We only want it to run for a short bit then stop
        var executeTask = service.StartAsync(cts.Token);

        // Wait a bit for it to process some pairs
        await Task.Delay(500);

        await service.StopAsync(CancellationToken.None);

        // Assert
        // TopPairs has 20 pairs. It should have called GetLiveRateAsync for at least some of them.
        rateServiceMock.Verify(s => s.GetLiveRateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task SnapshotProcessingService_ContinuesAfterFailure()
    {
        // Arrange
        var queueMock = new Mock<ISnapshotQueue>();
        var batchId1 = Guid.NewGuid().ToString();
        var batchId2 = Guid.NewGuid().ToString();

        // Dequeue batch 1, then batch 2, then wait
        queueMock.SetupSequence(q => q.DequeueAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(batchId1)
            .ReturnsAsync(batchId2)
            .Returns(async () => { await Task.Delay(-1); return ""; }); // Hang after 2

        var mockServiceProvider = new Mock<IServiceProvider>();
        var mockScope = new Mock<IServiceScope>();
        var mockScopeFactory = new Mock<IServiceScopeFactory>();

        mockServiceProvider.Setup(s => s.GetService(typeof(IServiceScopeFactory))).Returns(mockScopeFactory.Object);
        mockScopeFactory.Setup(s => s.CreateScope()).Returns(mockScope.Object);
        mockScope.Setup(s => s.ServiceProvider).Returns(mockServiceProvider.Object);

        // Fail for batchId1 when creating scope or getting context
        mockServiceProvider.Setup(s => s.GetService(typeof(Maliev.CurrencyService.Data.CurrencyDbContext)))
            .Returns<Type>(t =>
            {
                // This is called inside ProcessBatchAsync
                throw new Exception("Processing Failure");
            });

        var loggerMock = new Mock<ILogger<SnapshotProcessingService>>();
        var service = new SnapshotProcessingService(queueMock.Object, mockServiceProvider.Object, loggerMock.Object);

        // Act
        var cts = new CancellationTokenSource();
        var executeTask = service.StartAsync(cts.Token);

        await Task.Delay(500);
        cts.Cancel();
        try { await executeTask; } catch (OperationCanceledException) { }

        // Assert
        // Should have dequeued twice
        queueMock.Verify(q => q.DequeueAsync(It.IsAny<CancellationToken>()), Times.AtLeast(2));
        // Should have logged error for first failure
        loggerMock.Verify(x => x.Log(LogLevel.Error, It.IsAny<EventId>(), It.IsAny<It.IsAnyType>(), It.IsAny<Exception>(), It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.AtLeastOnce);
    }
}
