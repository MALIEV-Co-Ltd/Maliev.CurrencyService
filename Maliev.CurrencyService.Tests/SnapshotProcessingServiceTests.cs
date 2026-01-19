using Maliev.CurrencyService.Api.BackgroundServices;
using Maliev.CurrencyService.Api.Services;
using Maliev.CurrencyService.Data;
using Maliev.CurrencyService.Data.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Maliev.CurrencyService.Tests;

public class SnapshotProcessingServiceTests
{
    [Fact]
    public async Task SnapshotProcessingService_ProcessesBatch()
    {
        // Arrange
        var services = new ServiceCollection();
        var options = new DbContextOptionsBuilder<CurrencyDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        services.AddScoped(_ => new CurrencyDbContext(options));
        var serviceProvider = services.BuildServiceProvider();

        var queueMock = new Mock<ISnapshotQueue>();
        var loggerMock = new Mock<ILogger<SnapshotProcessingService>>();

        var batchId = Guid.NewGuid();
        queueMock.SetupSequence(q => q.DequeueAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(batchId.ToString())
            .ThrowsAsync(new OperationCanceledException()); // To break the loop

        // Add staged data
        using (var context = new CurrencyDbContext(options))
        {
            context.StagedSnapshots.Add(new StagedSnapshot
            {
                Id = Guid.NewGuid(),
                BatchId = batchId,
                FromCurrency = "USD",
                ToCurrency = "EUR",
                Rate = 0.85m,
                SnapshotDate = new DateOnly(2024, 1, 1),
                Status = "Validated",
                CreatedAt = DateTime.UtcNow
            });
            await context.SaveChangesAsync();
        }

        var service = new SnapshotProcessingService(queueMock.Object, serviceProvider, loggerMock.Object);

        // Act
        var cts = new CancellationTokenSource();
        await service.StartAsync(cts.Token);
        await Task.Delay(500); // Give it time to process
        await service.StopAsync(CancellationToken.None);

        // Assert
        queueMock.Verify(q => q.UpdateStatus(batchId.ToString(), "Completed", null), Times.Once);
    }
}
