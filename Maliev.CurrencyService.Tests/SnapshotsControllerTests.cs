using Maliev.CurrencyService.Api.Controllers;
using Maliev.CurrencyService.Api.Models.Snapshots;
using Maliev.CurrencyService.Application.DTOs.Snapshots;
using Maliev.CurrencyService.Application.Interfaces;
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
        var auditLog = new Maliev.CurrencyService.Application.DTOs.Snapshots.SnapshotAuditLog
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

    [Fact]
    public async Task GetBatchAudit_NotFound_ReturnsNotFound()
    {
        var batchId = Guid.NewGuid().ToString();
        _snapshotServiceMock.Setup(s => s.GetBatchAuditAsync(batchId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Maliev.CurrencyService.Application.DTOs.Snapshots.SnapshotAuditLog?)null);

        var result = await _controller.GetBatchAudit(batchId);

        var objectResult = Assert.IsAssignableFrom<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status404NotFound, objectResult.StatusCode);
    }

    [Fact]
    public async Task GetBatchAudit_Exception_Returns500()
    {
        _snapshotServiceMock.Setup(s => s.GetBatchAuditAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("DB error"));

        var result = await _controller.GetBatchAudit("some-id");

        var objectResult = Assert.IsAssignableFrom<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status500InternalServerError, objectResult.StatusCode);
    }

    [Fact]
    public async Task PromoteBatch_NotFound_ReturnsNotFound()
    {
        var batchId = Guid.NewGuid().ToString();
        _snapshotServiceMock.Setup(s => s.PromoteBatchAsync(batchId, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await _controller.PromoteBatch(batchId);

        var objectResult = Assert.IsAssignableFrom<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status404NotFound, objectResult.StatusCode);
    }

    [Fact]
    public async Task PromoteBatch_Exception_Returns500()
    {
        _snapshotServiceMock.Setup(s => s.PromoteBatchAsync(It.IsAny<string>(), null, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("DB error"));

        var result = await _controller.PromoteBatch("some-id");

        var objectResult = Assert.IsAssignableFrom<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status500InternalServerError, objectResult.StatusCode);
    }

    [Fact]
    public async Task CleanupOldSnapshots_Exception_Returns500()
    {
        _snapshotServiceMock.Setup(s => s.CleanupOldSnapshotsAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Storage error"));

        var result = await _controller.CleanupOldSnapshots();

        var objectResult = Assert.IsAssignableFrom<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status500InternalServerError, objectResult.StatusCode);
    }

    [Fact]
    public async Task ImportBatch_EmptyList_ReturnsBadRequest()
    {
        var result = await _controller.ImportBatch(new List<SnapshotEntryDto>());
        var objectResult = Assert.IsAssignableFrom<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, objectResult.StatusCode);
    }

    [Fact]
    public async Task ImportBatch_DryRun_ReturnsValidationReport()
    {
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

        var result = await _controller.ImportBatch(snapshots, dryRun: true);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var report = Assert.IsType<Maliev.CurrencyService.Api.Models.Snapshots.ValidationReport>(okResult.Value);
        Assert.True(report.IsValid);
        Assert.True(report.IsDryRun);
    }

    [Fact]
    public async Task ImportBatch_WithValidationErrors_ReturnsBadRequest()
    {
        var snapshots = new List<SnapshotEntryDto>
        {
            new() { From = "USD", To = "EUR", Rate = 0.85m, Timestamp = DateTime.UtcNow.ToString("O") }
        };
        var batchResponse = new SnapshotBatchResponse
        {
            BatchId = Guid.NewGuid().ToString(),
            SuccessCount = 0,
            FailureCount = 1,
            Status = "failed",
            SnapshotDate = DateOnly.FromDateTime(DateTime.UtcNow),
            ProcessedAt = DateTime.UtcNow,
            Source = "AdminApi",
            Errors = new Dictionary<string, string[]> { { "0", new[] { "Invalid entry" } } }
        };
        _snapshotServiceMock.Setup(s => s.ImportBatchAsync(It.IsAny<SnapshotBatchRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(batchResponse);

        var result = await _controller.ImportBatch(snapshots);

        var objectResult = Assert.IsAssignableFrom<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, objectResult.StatusCode);
    }

    [Fact]
    public async Task ImportBatch_Exception_Returns500()
    {
        var snapshots = new List<SnapshotEntryDto>
        {
            new() { From = "USD", To = "EUR", Rate = 0.85m, Timestamp = DateTime.UtcNow.ToString("O") }
        };
        _snapshotServiceMock.Setup(s => s.ImportBatchAsync(It.IsAny<SnapshotBatchRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("DB error"));

        var result = await _controller.ImportBatch(snapshots);

        var objectResult = Assert.IsAssignableFrom<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status500InternalServerError, objectResult.StatusCode);
    }
}
