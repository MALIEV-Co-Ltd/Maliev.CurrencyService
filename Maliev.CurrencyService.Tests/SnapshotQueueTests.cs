using Maliev.CurrencyService.Api.Services;
using Maliev.CurrencyService.Data;
using Maliev.CurrencyService.Data.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Maliev.CurrencyService.Tests;

public class SnapshotQueueTests
{
    [Fact]
    public async Task SnapshotQueue_QueueAndDequeue_Works()
    {
        // Arrange
        var services = new ServiceCollection();
        var options = new DbContextOptionsBuilder<CurrencyDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        services.AddScoped(_ => new CurrencyDbContext(options));
        var serviceProvider = services.BuildServiceProvider();

        var loggerMock = new Mock<ILogger<SnapshotQueue>>();
        var queue = new SnapshotQueue(serviceProvider, loggerMock.Object);
        var batchId = Guid.NewGuid().ToString();

        // Act
        await queue.QueueBackgroundWorkItemAsync(batchId);
        var dequeued = await queue.DequeueAsync(CancellationToken.None);

        // Assert
        Assert.Equal(batchId, dequeued);
        using (var context = new CurrencyDbContext(options))
        {
            var status = await context.BatchStatuses.FirstOrDefaultAsync(s => s.BatchId == batchId);
            Assert.NotNull(status);
            Assert.Equal("Queued", status.Status);
        }
    }

    [Fact]
    public async Task SnapshotQueue_UpdateStatus_Works()
    {
        // Arrange
        var services = new ServiceCollection();
        var options = new DbContextOptionsBuilder<CurrencyDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        services.AddScoped(_ => new CurrencyDbContext(options));
        var serviceProvider = services.BuildServiceProvider();

        var loggerMock = new Mock<ILogger<SnapshotQueue>>();
        var queue = new SnapshotQueue(serviceProvider, loggerMock.Object);
        var batchId = Guid.NewGuid().ToString();

        using (var context = new CurrencyDbContext(options))
        {
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
