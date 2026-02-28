using Maliev.CurrencyService.Api.BackgroundServices;
using Maliev.CurrencyService.Application.Interfaces;
using Maliev.CurrencyService.Domain.Entities;
using Maliev.CurrencyService.Infrastructure.Persistence;
using Maliev.CurrencyService.Tests.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Maliev.CurrencyService.Tests;

/// <summary>
/// Tests for <see cref="SnapshotProcessingService"/> using real PostgreSQL via Testcontainers.
/// </summary>
public class SnapshotProcessingServiceTests : IClassFixture<BaseIntegrationTestFactory<Program, CurrencyDbContext>>, IAsyncLifetime
{
    private readonly BaseIntegrationTestFactory<Program, CurrencyDbContext> _factory;

    /// <summary>Initializes a new instance of the <see cref="SnapshotProcessingServiceTests"/> class.</summary>
    public SnapshotProcessingServiceTests(BaseIntegrationTestFactory<Program, CurrencyDbContext> factory)
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

    /// <summary>SnapshotProcessingService processes a queued batch successfully.</summary>
    [Fact]
    public async Task SnapshotProcessingService_ProcessesBatch()
    {
        // Arrange
        var connectionString = _factory.Services.GetRequiredService<Microsoft.Extensions.Configuration.IConfiguration>()
            ["ConnectionStrings:CurrencyDbContext"]
            ?? throw new InvalidOperationException("CurrencyDbContext connection string not found.");

        var services = new ServiceCollection();
        services.AddDbContext<CurrencyDbContext>(options => options.UseNpgsql(connectionString));
        var serviceProvider = services.BuildServiceProvider();

        var queueMock = new Mock<ISnapshotQueue>();
        var loggerMock = new Mock<ILogger<SnapshotProcessingService>>();

        var batchId = Guid.NewGuid();
        queueMock.SetupSequence(q => q.DequeueAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(batchId.ToString())
            .ThrowsAsync(new OperationCanceledException()); // To break the loop

        // Add staged data
        using (var scope = serviceProvider.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<CurrencyDbContext>();
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
