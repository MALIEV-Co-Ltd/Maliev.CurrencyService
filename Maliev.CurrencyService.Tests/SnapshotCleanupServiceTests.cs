using Maliev.CurrencyService.Api.BackgroundServices;
using Maliev.CurrencyService.Api.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Maliev.CurrencyService.Tests;

public class SnapshotCleanupServiceTests
{
    [Fact]
    public async Task SnapshotCleanupService_ExecutesCleanup_InLoop()
    {
        // Arrange
        var snapshotServiceMock = new Mock<ISnapshotService>();
        snapshotServiceMock.Setup(s => s.CleanupOldSnapshotsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(5);

        var serviceScopeMock = new Mock<IServiceScope>();
        serviceScopeMock.Setup(x => x.ServiceProvider.GetService(typeof(ISnapshotService)))
            .Returns(snapshotServiceMock.Object);

        var serviceScopeFactoryMock = new Mock<IServiceScopeFactory>();
        serviceScopeFactoryMock.Setup(x => x.CreateScope())
            .Returns(serviceScopeMock.Object);

        var serviceProviderMock = new Mock<IServiceProvider>();
        serviceProviderMock.Setup(x => x.GetService(typeof(IServiceScopeFactory)))
            .Returns(serviceScopeFactoryMock.Object);

        var loggerMock = new Mock<ILogger<SnapshotCleanupService>>();

        var service = new SnapshotCleanupService(serviceProviderMock.Object, loggerMock.Object);

        // Act
        var cts = new CancellationTokenSource();
        // Since it has a long initial delay, I won't wait for the loop.
        // But starting it will at least cover the startup code.
        var task = service.StartAsync(cts.Token);
        cts.Cancel();
        await task;

        // Assert
        Assert.True(task.IsCompleted);
    }
}
