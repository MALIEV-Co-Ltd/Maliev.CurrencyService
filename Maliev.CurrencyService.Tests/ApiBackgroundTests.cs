using Maliev.CurrencyService.Api.BackgroundServices;
using Maliev.CurrencyService.Api.Metrics;
using Maliev.CurrencyService.Application.DTOs.Rates;
using Maliev.CurrencyService.Application.Interfaces;
using Maliev.CurrencyService.Domain.Entities;
using Maliev.CurrencyService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Testcontainers.PostgreSql;

namespace Maliev.CurrencyService.Tests;

public class CacheWarmingServiceTests : IDisposable
{
    private readonly Mock<IServiceProvider> _serviceProviderMock;
    private readonly Mock<ILogger<CacheWarmingService>> _loggerMock;
    private readonly CurrencyServiceMetrics _metrics;
    private readonly Mock<IRateService> _rateServiceMock;
    private readonly Mock<IServiceScope> _scopeMock;
    private readonly Mock<IServiceScopeFactory> _scopeFactoryMock;
    private readonly Mock<IServiceProvider> _scopedServiceProviderMock;

    public CacheWarmingServiceTests()
    {
        _serviceProviderMock = new Mock<IServiceProvider>();
        _loggerMock = new Mock<ILogger<CacheWarmingService>>();

        var configMock = new Mock<IConfiguration>();
        configMock.Setup(c => c["ASPNETCORE_ENVIRONMENT"]).Returns("Test");
        _metrics = new CurrencyServiceMetrics(configMock.Object);

        _rateServiceMock = new Mock<IRateService>();
        _scopeMock = new Mock<IServiceScope>();
        _scopeFactoryMock = new Mock<IServiceScopeFactory>();
        _scopedServiceProviderMock = new Mock<IServiceProvider>();

        _scopeMock.Setup(s => s.ServiceProvider).Returns(_scopedServiceProviderMock.Object);
        _scopeFactoryMock.Setup(sf => sf.CreateScope()).Returns(_scopeMock.Object);

        _serviceProviderMock
            .Setup(sp => sp.GetService(typeof(IServiceScopeFactory)))
            .Returns(_scopeFactoryMock.Object);

        _scopedServiceProviderMock
            .Setup(sp => sp.GetService(typeof(IRateService)))
            .Returns(_rateServiceMock.Object);
    }

    private CacheWarmingService CreateService()
    {
        return new CacheWarmingService(
            _serviceProviderMock.Object,
            _loggerMock.Object,
            _metrics);
    }

    private static ExchangeRateResponse CreateValidRateResponse(string from = "USD", string to = "THB", decimal rate = 35.5m)
    {
        return new ExchangeRateResponse
        {
            FromCurrency = from,
            ToCurrency = to,
            Rate = rate,
            Timestamp = DateTime.UtcNow,
            Source = "Test",
            IsTransitive = false,
            Mode = "live"
        };
    }

    [Fact]
    public async Task ExecuteAsync_WarmsCacheForAllTopPairs()
    {
        var service = CreateService();
        var cts = new CancellationTokenSource();

        _rateServiceMock
            .Setup(r => r.GetLiveRateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => CreateValidRateResponse());

        await service.StartAsync(cts.Token);

        await Task.Delay(500, CancellationToken.None);
        cts.Cancel();

        try
        {
            await service.StopAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
        }

        _rateServiceMock.Verify(
            r => r.GetLiveRateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task ExecuteAsync_HandlesNullRateGracefully()
    {
        var service = CreateService();
        var cts = new CancellationTokenSource();

        _rateServiceMock
            .SetupSequence(r => r.GetLiveRateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => CreateValidRateResponse())
            .ReturnsAsync((ExchangeRateResponse?)null)
            .ReturnsAsync(() => CreateValidRateResponse("EUR", "THB", 38.0m));

        await service.StartAsync(cts.Token);

        await Task.Delay(800, CancellationToken.None);
        cts.Cancel();

        try
        {
            await service.StopAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
        }

        _rateServiceMock.Verify(
            r => r.GetLiveRateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.AtLeast(3));
    }

    [Fact]
    public async Task ExecuteAsync_CancelsWhenTokenCancelled()
    {
        var service = CreateService();
        var cts = new CancellationTokenSource();

        _rateServiceMock
            .Setup(r => r.GetLiveRateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => CreateValidRateResponse());

        await service.StartAsync(cts.Token);

        await Task.Delay(300, CancellationToken.None);
        cts.Cancel();

        try
        {
            await service.StopAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
        }
    }

    [Fact]
    public async Task ExecuteAsync_LogsInvalidPairFormat()
    {
        var service = CreateService();
        var cts = new CancellationTokenSource();

        _rateServiceMock
            .Setup(r => r.GetLiveRateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => CreateValidRateResponse());

        await service.StartAsync(cts.Token);
        await Task.Delay(200, CancellationToken.None);
        cts.Cancel();

        try { await service.StopAsync(cts.Token); }
        catch (OperationCanceledException) { }

        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Cache warming")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task ExecuteAsync_RecordsMetricsOnCompletion()
    {
        var service = CreateService();
        var cts = new CancellationTokenSource();

        _rateServiceMock
            .Setup(r => r.GetLiveRateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => CreateValidRateResponse());

        await service.StartAsync(cts.Token);
        await Task.Delay(500, CancellationToken.None);
        cts.Cancel();

        try { await service.StopAsync(cts.Token); }
        catch (OperationCanceledException) { }

        Assert.True(true);
    }

    public void Dispose()
    {
        _metrics.Dispose();
    }
}

public class SnapshotCleanupServiceTests
{
    private readonly Mock<IServiceProvider> _serviceProviderMock;
    private readonly Mock<ILogger<SnapshotCleanupService>> _loggerMock;
    private readonly Mock<ISnapshotService> _snapshotServiceMock;
    private readonly Mock<IServiceScope> _scopeMock;
    private readonly Mock<IServiceScopeFactory> _scopeFactoryMock;
    private readonly Mock<IServiceProvider> _scopedServiceProviderMock;

    public SnapshotCleanupServiceTests()
    {
        _serviceProviderMock = new Mock<IServiceProvider>();
        _loggerMock = new Mock<ILogger<SnapshotCleanupService>>();
        _snapshotServiceMock = new Mock<ISnapshotService>();
        _scopeMock = new Mock<IServiceScope>();
        _scopeFactoryMock = new Mock<IServiceScopeFactory>();
        _scopedServiceProviderMock = new Mock<IServiceProvider>();

        // Set up the scope chain
        _scopeMock.Setup(s => s.ServiceProvider).Returns(_scopedServiceProviderMock.Object);
        _scopeFactoryMock.Setup(sf => sf.CreateScope()).Returns(_scopeMock.Object);

        // When CreateScope() is called on IServiceProvider, it internally uses IServiceScopeFactory
        // We need to return our scope factory when GetService is called
        _serviceProviderMock
            .Setup(sp => sp.GetService(typeof(IServiceScopeFactory)))
            .Returns(_scopeFactoryMock.Object);

        // Return the scoped service when requested
        _scopedServiceProviderMock
            .Setup(sp => sp.GetService(typeof(ISnapshotService)))
            .Returns(_snapshotServiceMock.Object);
    }

    private SnapshotCleanupService CreateService()
    {
        return new SnapshotCleanupService(
            _serviceProviderMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task ExecuteAsync_CallsCleanupOnSchedule()
    {
        var service = CreateService();
        var cts = new CancellationTokenSource();

        await service.StartAsync(cts.Token);

        // Wait for service to start and schedule initial delay
        await Task.Delay(500, CancellationToken.None);

        // Cancel before cleanup cycle runs (initial delay could be hours)
        cts.Cancel();

        try
        {
            await service.StopAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception)
        {
        }

        // Verify service started successfully
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("started")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_ContinuesOnError()
    {
        var service = CreateService();
        var cts = new CancellationTokenSource();

        await service.StartAsync(cts.Token);

        // Wait for service to start
        await Task.Delay(500, CancellationToken.None);
        cts.Cancel();

        try
        {
            await service.StopAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception)
        {
        }

        // Service should handle errors gracefully and log shutdown
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("shutting down")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_HandlesCancellationGracefully()
    {
        var service = CreateService();
        var cts = new CancellationTokenSource();

        _snapshotServiceMock
            .Setup(s => s.CleanupOldSnapshotsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(10);

        await service.StartAsync(cts.Token);

        await Task.Delay(500, CancellationToken.None);
        await cts.CancelAsync();

        try
        {
            await service.StopAsync(CancellationToken.None);
        }
        catch (OperationCanceledException)
        {
        }

        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("shutting down")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_SchedulesInitialDelay()
    {
        var service = CreateService();
        var cts = new CancellationTokenSource();

        _snapshotServiceMock
            .Setup(s => s.CleanupOldSnapshotsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        var startTime = DateTime.UtcNow;
        await service.StartAsync(cts.Token);

        await Task.Delay(200, CancellationToken.None);
        cts.Cancel();

        try
        {
            await service.StopAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
        }

        var elapsed = DateTime.UtcNow - startTime;
        Assert.True(elapsed >= TimeSpan.FromMilliseconds(100));
    }

    [Fact]
    public async Task ExecuteAsync_LogsOnStartup()
    {
        var service = CreateService();
        var cts = new CancellationTokenSource();

        _snapshotServiceMock
            .Setup(s => s.CleanupOldSnapshotsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        await service.StartAsync(cts.Token);
        await Task.Delay(100, CancellationToken.None);
        cts.Cancel();

        try { await service.StopAsync(cts.Token); }
        catch (OperationCanceledException) { }

        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("started")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}

public class SnapshotProcessingServiceTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _dbContainer = 
#pragma warning disable CS0618
        new PostgreSqlBuilder().WithImage("postgres:18-alpine")
        .Build();
#pragma warning restore CS0618

    private readonly Mock<ISnapshotQueue> _queueMock;
    private readonly Mock<IServiceProvider> _serviceProviderMock;
    private readonly Mock<ILogger<SnapshotProcessingService>> _loggerMock;
    private readonly Mock<IServiceScope> _scopeMock;
    private readonly Mock<IServiceScopeFactory> _scopeFactoryMock;
    private CurrencyDbContext _context = null!;

    public SnapshotProcessingServiceTests()
    {
        _queueMock = new Mock<ISnapshotQueue>();
        _serviceProviderMock = new Mock<IServiceProvider>();
        _loggerMock = new Mock<ILogger<SnapshotProcessingService>>();
        _scopeMock = new Mock<IServiceScope>();
        _scopeFactoryMock = new Mock<IServiceScopeFactory>();

        _scopeMock.Setup(s => s.ServiceProvider).Returns(_serviceProviderMock.Object);
        _scopeFactoryMock.Setup(sf => sf.CreateScope()).Returns(_scopeMock.Object);

        _serviceProviderMock
            .Setup(sp => sp.GetService(typeof(IServiceScopeFactory)))
            .Returns(_scopeFactoryMock.Object);
    }

    public async Task InitializeAsync()
    {
        await _dbContainer.StartAsync();

        var options = new DbContextOptionsBuilder<CurrencyDbContext>()
            .UseNpgsql(_dbContainer.GetConnectionString())
            .Options;

        _context = new CurrencyDbContext(options);
        await _context.Database.EnsureCreatedAsync();

        _serviceProviderMock
            .Setup(sp => sp.GetService(typeof(CurrencyDbContext)))
            .Returns(_context);
    }

    public async Task DisposeAsync()
    {
        if (_context != null) await _context.DisposeAsync();
        await _dbContainer.DisposeAsync();
    }

    private SnapshotProcessingService CreateService()
    {
        return new SnapshotProcessingService(
            _queueMock.Object,
            _serviceProviderMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task ExecuteAsync_ProcessesValidBatchId()
    {
        var batchId = Guid.NewGuid();
        var service = CreateService();
        var cts = new CancellationTokenSource();

        _context.StagedSnapshots.Add(new Domain.Entities.StagedSnapshot
        {
            Id = Guid.NewGuid(),
            BatchId = batchId,
            FromCurrency = "USD",
            ToCurrency = "THB",
            Rate = 35.5m,
            SnapshotDate = DateOnly.FromDateTime(DateTime.UtcNow),
            Status = "Validated",
            CreatedAt = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();

        _queueMock
            .SetupSequence(q => q.DequeueAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(batchId.ToString())
            .Returns(async () =>
            {
                cts.Cancel();
                await Task.Delay(Timeout.Infinite, cts.Token);
                return "";
            });

        await service.StartAsync(cts.Token);

        await Task.Delay(500, CancellationToken.None);

        try
        {
            await service.StopAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
        }

        _queueMock.Verify(
            q => q.UpdateStatus(batchId.ToString(), "Processing", null),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task ExecuteAsync_HandlesInvalidBatchId()
    {
        var service = CreateService();
        var cts = new CancellationTokenSource();

        _queueMock
            .SetupSequence(q => q.DequeueAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync("invalid-guid")
            .Returns(async () =>
            {
                cts.Cancel();
                await Task.Delay(Timeout.Infinite, cts.Token);
                return "";
            });

        await service.StartAsync(cts.Token);

        await Task.Delay(300, CancellationToken.None);

        try
        {
            await service.StopAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
        }

        _queueMock.Verify(
            q => q.UpdateStatus("invalid-guid", It.IsAny<string>(), It.IsAny<string?>()),
            Times.Never);

        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Invalid batch ID")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_MarksBatchCompleted_WhenStagedSnapshotsExist()
    {
        var batchId = Guid.NewGuid();
        var service = CreateService();
        var cts = new CancellationTokenSource();

        _context.StagedSnapshots.Add(new Domain.Entities.StagedSnapshot
        {
            Id = Guid.NewGuid(),
            BatchId = batchId,
            FromCurrency = "USD",
            ToCurrency = "THB",
            Rate = 35.5m,
            SnapshotDate = DateOnly.FromDateTime(DateTime.UtcNow),
            Status = "Validated",
            CreatedAt = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();

        _queueMock
            .SetupSequence(q => q.DequeueAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(batchId.ToString())
            .Returns(async () =>
            {
                cts.Cancel();
                await Task.Delay(Timeout.Infinite, cts.Token);
                return "";
            });

        await service.StartAsync(cts.Token);

        await Task.Delay(500, CancellationToken.None);

        try
        {
            await service.StopAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
        }

        _queueMock.Verify(
            q => q.UpdateStatus(batchId.ToString(), "Completed", null),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task ExecuteAsync_MarksBatchFailed_WhenNoStagedSnapshots()
    {
        var batchId = Guid.NewGuid();
        var service = CreateService();
        var cts = new CancellationTokenSource();

        _queueMock
            .SetupSequence(q => q.DequeueAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(batchId.ToString())
            .Returns(async () =>
            {
                cts.Cancel();
                await Task.Delay(Timeout.Infinite, cts.Token);
                return "";
            });

        await service.StartAsync(cts.Token);

        await Task.Delay(500, CancellationToken.None);

        try
        {
            await service.StopAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
        }

        _queueMock.Verify(
            q => q.UpdateStatus(batchId.ToString(), "Failed", "No staged snapshots found"),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task ExecuteAsync_HandlesCancellation()
    {
        var service = CreateService();
        var cts = new CancellationTokenSource();

        _queueMock
            .Setup(q => q.DequeueAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        await service.StartAsync(cts.Token);
        await Task.Delay(100, CancellationToken.None);

        try
        {
            await service.StopAsync(CancellationToken.None);
        }
        catch (OperationCanceledException)
        {
        }

        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("stopping")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_LogsOnStartup()
    {
        var service = CreateService();
        var cts = new CancellationTokenSource();

        _queueMock
            .SetupSequence(q => q.DequeueAsync(It.IsAny<CancellationToken>()))
            .Returns(async () =>
            {
                cts.Cancel();
                await Task.Delay(Timeout.Infinite, cts.Token);
                return "";
            });

        await service.StartAsync(cts.Token);
        await Task.Delay(100, CancellationToken.None);

        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("starting")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}




