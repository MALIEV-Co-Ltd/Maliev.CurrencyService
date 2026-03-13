using Maliev.CurrencyService.Application.Interfaces;
using Maliev.CurrencyService.Infrastructure.Persistence;
using Maliev.CurrencyService.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Testcontainers.PostgreSql;

namespace Maliev.CurrencyService.Tests;

public class SnapshotQueueUnitTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _dbContainer = 
                #pragma warning disable CS0618
        new PostgreSqlBuilder().WithImage("postgres:18-alpine")
        .Build();
#pragma warning restore CS0618

    private readonly Mock<IServiceProvider> _serviceProviderMock;
    private readonly Mock<ILogger<SnapshotQueue>> _loggerMock;
    private readonly Mock<IServiceScopeFactory> _scopeFactoryMock;
    private readonly Mock<IServiceScope> _scopeMock;
    private CurrencyDbContext _context = null!;

    public SnapshotQueueUnitTests()
    {
        _serviceProviderMock = new Mock<IServiceProvider>();
        _loggerMock = new Mock<ILogger<SnapshotQueue>>();

        _scopeMock = new Mock<IServiceScope>();
        _scopeMock.Setup(s => s.ServiceProvider).Returns(_serviceProviderMock.Object);

        _scopeFactoryMock = new Mock<IServiceScopeFactory>();
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

    private SnapshotQueue CreateService()
    {
        return new SnapshotQueue(_serviceProviderMock.Object, _loggerMock.Object);
    }

    [Fact]
    public async Task QueueBackgroundWorkItemAsync_ThrowsArgumentNullException_WhenBatchIdIsNull()
    {
        var service = CreateService();

        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await service.QueueBackgroundWorkItemAsync(null!));
    }

    [Fact]
    public async Task QueueBackgroundWorkItemAsync_ThrowsArgumentNullException_WhenBatchIdIsEmpty()
    {
        var service = CreateService();

        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await service.QueueBackgroundWorkItemAsync(""));
    }

    [Fact]
    public async Task QueueBackgroundWorkItemAsync_AllowsWhitespace()
    {
        var service = CreateService();

        var exception = await Record.ExceptionAsync(async () =>
            await service.QueueBackgroundWorkItemAsync("   "));

        Assert.Null(exception);
    }

    [Fact]
    public async Task DequeueAsync_ReturnsQueuedItem()
    {
        var batchId = "test-batch-123";

        var service = CreateService();

        await service.QueueBackgroundWorkItemAsync(batchId);

        var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromSeconds(2));

        var result = await service.DequeueAsync(cts.Token);

        Assert.Equal(batchId, result);
    }

    [Fact]
    public void UpdateStatus_AcceptsValidParameters()
    {
        var service = CreateService();

        var exception = Record.Exception(() =>
            service.UpdateStatus("batch-1", "Processing"));

        Assert.Null(exception);
    }

    [Fact]
    public void UpdateStatus_AcceptsNullError()
    {
        var service = CreateService();

        var exception = Record.Exception(() =>
            service.UpdateStatus("batch-1", "Completed", null));

        Assert.Null(exception);
    }

    [Fact]
    public void GetStatus_ReturnsNotFound_WhenBatchIdDoesNotExist()
    {
        var service = CreateService();

        var (status, error) = service.GetStatus("non-existent-batch");

        Assert.Equal("NotFound", status);
        Assert.Null(error);
    }
}




