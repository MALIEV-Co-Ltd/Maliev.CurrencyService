using Maliev.CurrencyService.Application.Interfaces;
using Maliev.CurrencyService.Domain.Entities;
using Maliev.CurrencyService.Infrastructure.Persistence;
using Maliev.CurrencyService.Infrastructure.Services;
using Maliev.CurrencyService.Tests.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Maliev.CurrencyService.Tests;

/// <summary>
/// Tests for <see cref="SnapshotQueue"/> using real PostgreSQL via Testcontainers.
/// </summary>
public class SnapshotQueueTests : IClassFixture<BaseIntegrationTestFactory<Program, CurrencyDbContext>>, IAsyncLifetime
{
    private readonly BaseIntegrationTestFactory<Program, CurrencyDbContext> _factory;

    /// <summary>Initializes a new instance of the <see cref="SnapshotQueueTests"/> class.</summary>
    public SnapshotQueueTests(BaseIntegrationTestFactory<Program, CurrencyDbContext> factory)
    {
        _factory = factory;
    }

    /// <inheritdoc/>
    public async Task InitializeAsync()
    {
        await _factory.CleanDatabaseAsync();
    }

    /// <inheritdoc/>
    public Task DisposeAsync() => Task.CompletedTask;

    /// <summary>Queue and dequeue works correctly.</summary>
    [Fact]
    public async Task SnapshotQueue_QueueAndDequeue_Works()
    {
        // Arrange
        var connectionString = _factory.Services.GetRequiredService<Microsoft.Extensions.Configuration.IConfiguration>()
            ["ConnectionStrings:CurrencyDbContext"]
            ?? throw new InvalidOperationException("CurrencyDbContext connection string not found.");

        var services = new ServiceCollection();
        services.AddDbContext<CurrencyDbContext>(options => options.UseNpgsql(connectionString));
        var serviceProvider = services.BuildServiceProvider();

        var loggerMock = new Mock<ILogger<SnapshotQueue>>();
        var queue = new SnapshotQueue(serviceProvider, loggerMock.Object);
        var batchId = Guid.NewGuid().ToString();

        // Act
        await queue.QueueBackgroundWorkItemAsync(batchId);
        var dequeued = await queue.DequeueAsync(CancellationToken.None);

        // Assert
        Assert.Equal(batchId, dequeued);
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<CurrencyDbContext>();
        var status = await context.BatchStatuses.FirstOrDefaultAsync(s => s.BatchId == batchId);
        Assert.NotNull(status);
        Assert.Equal("Queued", status.Status);
    }

    /// <summary>UpdateStatus transitions correctly.</summary>
    [Fact]
    public async Task SnapshotQueue_UpdateStatus_Works()
    {
        // Arrange
        var connectionString = _factory.Services.GetRequiredService<Microsoft.Extensions.Configuration.IConfiguration>()
            ["ConnectionStrings:CurrencyDbContext"]
            ?? throw new InvalidOperationException("CurrencyDbContext connection string not found.");

        var services = new ServiceCollection();
        services.AddDbContext<CurrencyDbContext>(options => options.UseNpgsql(connectionString));
        var serviceProvider = services.BuildServiceProvider();

        var loggerMock = new Mock<ILogger<SnapshotQueue>>();
        var queue = new SnapshotQueue(serviceProvider, loggerMock.Object);
        var batchId = Guid.NewGuid().ToString();

        using (var scope = serviceProvider.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<CurrencyDbContext>();
            context.BatchStatuses.Add(new BatchStatus { Id = Guid.NewGuid(), BatchId = batchId, Status = "Queued", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
            await context.SaveChangesAsync();
        }

        // Act
        queue.UpdateStatus(batchId, "Completed");

        // Wait for fire-and-forget task
        await Task.Delay(200);

        // Assert
        var result = queue.GetStatus(batchId);
        Assert.Equal("Completed", result.Status);
    }
}
