using Maliev.CurrencyService.Api.Controllers;
using Maliev.CurrencyService.Api.Models.Snapshots;
using Maliev.CurrencyService.Api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Maliev.CurrencyService.Tests;

public class SnapshotsControllerTests
{
    private readonly Mock<ISnapshotService> _snapshotServiceMock;
    private readonly Mock<ISnapshotQueue> _snapshotQueueMock;
    private readonly Mock<ILogger<SnapshotsController>> _loggerMock;
    private readonly SnapshotsController _controller;

    public SnapshotsControllerTests()
    {
        _snapshotServiceMock = new Mock<ISnapshotService>();
        _snapshotQueueMock = new Mock<ISnapshotQueue>();
        _loggerMock = new Mock<ILogger<SnapshotsController>>();
        _controller = new SnapshotsController(_snapshotServiceMock.Object, _snapshotQueueMock.Object, _loggerMock.Object);
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };
    }

    [Fact]
    public async Task ImportBatch_ReturnsAccepted()
    {
        // Arrange
        var snapshots = new List<SnapshotEntryDto>
        {
            new() { From = "USD", To = "EUR", Rate = 0.85m, Timestamp = DateTime.UtcNow.ToString("O") }
        };
        var batchResponse = new SnapshotBatchResponse
        {
            BatchId = Guid.NewGuid().ToString(),
            SuccessCount = 1,
            FailureCount = 0,
            Status = "staged",
            SnapshotDate = DateOnly.FromDateTime(DateTime.UtcNow),
            ProcessedAt = DateTime.UtcNow,
            Source = "AdminApi"
        };

        _snapshotServiceMock.Setup(s => s.ImportBatchAsync(It.IsAny<SnapshotBatchRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(batchResponse);

        // Act
        var result = await _controller.ImportBatch(snapshots);

        // Assert
        var acceptedResult = Assert.IsType<AcceptedResult>(result);
        var ingestionResult = Assert.IsType<Maliev.CurrencyService.Api.Models.Snapshots.SnapshotIngestionResult>(acceptedResult.Value);
        Assert.Equal(batchResponse.BatchId, ingestionResult.BatchId);
    }

    [Fact]
    public async Task PromoteBatch_ReturnsOk()
    {
        // Arrange
        var batchId = Guid.NewGuid().ToString();
        _snapshotServiceMock.Setup(s => s.PromoteBatchAsync(batchId, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _controller.PromoteBatch(batchId);

        // Assert
        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task CleanupOldSnapshots_ReturnsOk()
    {
        // Arrange
        _snapshotServiceMock.Setup(s => s.CleanupOldSnapshotsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(10);

        // Act
        var result = await _controller.CleanupOldSnapshots();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public void GetBatchStatus_ReturnsOk()
    {
        // Arrange
        var batchId = Guid.NewGuid().ToString();
        _snapshotQueueMock.Setup(q => q.GetStatus(batchId))
            .Returns(("Completed", null));

        // Act
        var result = _controller.GetBatchStatus(batchId);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task GetBatchAudit_ReturnsOk()
    {
        // Arrange
        var batchId = Guid.NewGuid().ToString();
        var auditLog = new Maliev.CurrencyService.Api.Models.Snapshots.SnapshotAuditLog
        {
            BatchId = batchId,
            Timestamp = DateTime.UtcNow,
            RecordCount = 10,
            Source = "Manual",
            SubmittedBy = "Admin"
        };
        _snapshotServiceMock.Setup(s => s.GetBatchAuditAsync(batchId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(auditLog);

        // Act
        var result = await _controller.GetBatchAudit(batchId);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(auditLog, okResult.Value);
    }
}
