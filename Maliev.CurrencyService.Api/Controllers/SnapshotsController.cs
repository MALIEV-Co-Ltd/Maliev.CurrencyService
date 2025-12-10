using Asp.Versioning;
using Maliev.CurrencyService.Api.Models.Common;
using Maliev.CurrencyService.Api.Models.Snapshots;
using Maliev.CurrencyService.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Maliev.CurrencyService.Api.Controllers;

/// <summary>
/// Admin endpoints for snapshot batch management
/// </summary>
/// <remarks>
/// User Story 4: Provides bulk snapshot ingestion with staging, promotion, and cleanup operations.
/// All endpoints require JWT authentication (admin role).
/// </remarks>
[ApiController]
[ApiVersion("1.0")]
[Route("currencies/v{version:apiVersion}/admin/snapshots")]
[Produces("application/json")]
[Authorize] // Requires JWT authentication
public class SnapshotsController : ControllerBase
{
    private readonly ISnapshotService _snapshotService;
    private readonly ISnapshotQueue _snapshotQueue;
    private readonly ILogger<SnapshotsController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SnapshotsController"/> class.
    /// </summary>
    /// <param name="snapshotService">The snapshot service.</param>
    /// <param name="snapshotQueue">The snapshot queue</param>
    /// <param name="logger">The logger.</param>
    public SnapshotsController(
        ISnapshotService snapshotService,
        ISnapshotQueue snapshotQueue,
        ILogger<SnapshotsController> logger)
    {
        _snapshotService = snapshotService;
        _snapshotQueue = snapshotQueue;
        _logger = logger;
    }

    /// <summary>
    /// Import batch of exchange rate snapshots (FR-026)
    /// </summary>
    /// <param name="snapshots">Array of snapshot entries</param>
    /// <param name="dryRun">If true, validates without applying (FR-028)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Batch operation result</returns>
    /// <remarks>
    /// FR-026: Accepts JSON array format: [{"from":"USD","to":"EUR","rate":0.85,"timestamp":"2025-01-15T00:00:00Z"}]
    /// FR-027: Processes ingestion asynchronously via background jobs
    /// FR-028: Supports dry-run mode for validation without applying changes
    /// </remarks>
    [HttpPost("ingest")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> ImportBatch(
        [FromBody] List<SnapshotEntryDto> snapshots,
        [FromQuery] bool dryRun = false,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Validate input
            if (snapshots == null || snapshots.Count == 0)
            {
                return BadRequest(new ErrorResponse
                {
                    Error = "BadRequest",
                    Message = "Snapshot array cannot be empty",
                    Timestamp = DateTime.UtcNow,
                    CorrelationId = HttpContext.TraceIdentifier
                });
            }

            // Convert DTOs to internal Request
            var request = new SnapshotBatchRequest
            {
                Snapshots = snapshots.Select(s => new SnapshotEntry 
                { 
                    From = s.From, 
                    To = s.To, 
                    Rate = s.Rate 
                }).ToList(),
                Source = "AdminApi",
                AutoPromote = false, // Staging mode by default
                SnapshotDate = DateOnly.FromDateTime(DateTime.UtcNow) // Default to today, or extract from entries
            };

            // Use the date from the first entry if available
            if (snapshots.Any() && DateTime.TryParse(snapshots.First().Timestamp, out var ts))
            {
                request.SnapshotDate = DateOnly.FromDateTime(ts);
            }

            // Call Service
            // For DryRun, we just validate using ImportBatchAsync logic but don't persist?
            // Actually SnapshotService.ImportBatchAsync persists to Staging.
            // If DryRun is true, we should probably just Validate.
            // But the current controller code did custom validation.
            // Let's keep the custom validation for DryRun as per original logic to avoid breaking FR-028 check
            
            // Validate each snapshot entry (Controller Validation Layer)
            var validationErrors = new List<string>();
            for (int i = 0; i < snapshots.Count; i++)
            {
                var entry = snapshots[i];
                if (string.IsNullOrWhiteSpace(entry.From) || entry.From.Length != 3)
                    validationErrors.Add($"Entry {i}: Invalid 'from' currency code");
                if (string.IsNullOrWhiteSpace(entry.To) || entry.To.Length != 3)
                    validationErrors.Add($"Entry {i}: Invalid 'to' currency code");
                if (entry.Rate <= 0)
                    validationErrors.Add($"Entry {i}: Rate must be positive");

                if (string.IsNullOrWhiteSpace(entry.Timestamp) ||
                    !DateTime.TryParse(entry.Timestamp, out _))
                {
                    validationErrors.Add($"Entry {i}: Invalid timestamp format");
                }
            }

            _logger.LogInformation("POST /v1/admin/snapshots/ingest - Count: {Count}, DryRun: {DryRun}",
                snapshots.Count, dryRun);

            // FR-028: Dry-run mode returns validation report
            if (dryRun)
            {
                var report = new ValidationReport
                {
                    IsValid = validationErrors.Count == 0,
                    ValidationErrors = validationErrors,
                    RecordCount = snapshots.Count,
                    IsDryRun = true
                };

                Response.Headers["X-Correlation-ID"] = HttpContext.TraceIdentifier;
                return Ok(report);
            }

            // FR-028a: Reject entire batch on any validation error
            if (validationErrors.Count > 0)
            {
                return BadRequest(new ErrorResponse
                {
                    Error = "BadRequest",
                    Message = "validation failed for snapshot batch",
                    Timestamp = DateTime.UtcNow,
                    CorrelationId = HttpContext.TraceIdentifier,
                    Details = new Dictionary<string, string[]>
                    {
                        { "snapshots", validationErrors.ToArray() }
                    }
                });
            }

            // FR-027: Process asynchronously via Service + Queue
            // Service stages the data
            var batchResponse = await _snapshotService.ImportBatchAsync(request, cancellationToken);
            
            if (batchResponse.FailureCount > 0 && batchResponse.Errors != null)
            {
                 // If service found validation errors (e.g. invalid currency codes), reject
                 // This effectively implements "All or Nothing" at the service layer too
                 return BadRequest(new ErrorResponse
                 {
                     Error = "BadRequest",
                     Message = "validation failed for snapshot batch",
                     Timestamp = DateTime.UtcNow,
                     CorrelationId = HttpContext.TraceIdentifier,
                     Details = batchResponse.Errors.ToDictionary(k => k.Key, v => v.Value)
                 });
            }

            // Queue for background proccesing (Promotion check / Finalization)
            await _snapshotQueue.QueueBackgroundWorkItemAsync(batchResponse.BatchId);

            var result = new SnapshotIngestionResult
            {
                BatchId = batchResponse.BatchId,
                Status = "Queued",
                RecordCount = batchResponse.SuccessCount,
                SubmittedAt = DateTime.UtcNow
            };

            _logger.LogInformation("Snapshot batch {BatchId} queued for processing: {Count} records",
                batchResponse.BatchId, snapshots.Count);

            Response.Headers["X-Correlation-ID"] = HttpContext.TraceIdentifier;
            return Accepted(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error importing snapshot batch");

            return StatusCode(StatusCodes.Status500InternalServerError, new ErrorResponse
            {
                Error = "InternalServerError",
                Message = "An error occurred while importing snapshot batch",
                Timestamp = DateTime.UtcNow,
                CorrelationId = HttpContext.TraceIdentifier
            });
        }
    }

    /// <summary>
    /// Promote staged batch to production
    /// </summary>
    /// <param name="batchId">Batch ID from import operation</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Success indicator</returns>
    /// <remarks>
    /// Moves snapshots from staging (validated status) to production RateSnapshot table.
    /// Invalidates affected cache keys and removes staging entries.
    /// </remarks>
    [HttpPost("{batchId}/promote")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> PromoteBatch(
        string batchId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("POST /v1/admin/snapshots/{BatchId}/promote", batchId);

            var success = await _snapshotService.PromoteBatchAsync(batchId, null, cancellationToken);

            if (!success)
            {
                return NotFound(new ErrorResponse
                {
                    Error = "NotFound",
                    Message = $"Batch {batchId} not found or has no validated snapshots",
                    Timestamp = DateTime.UtcNow,
                    CorrelationId = HttpContext.TraceIdentifier
                });
            }

            // Update status in queue tracker if present
            _snapshotQueue.UpdateStatus(batchId, "Promoted");

            Response.Headers["X-Correlation-ID"] = HttpContext.TraceIdentifier;

            return Ok(new
            {
                batchId,
                status = "promoted",
                message = "Batch promoted to production successfully",
                timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error promoting batch {BatchId}", batchId);

            return StatusCode(StatusCodes.Status500InternalServerError, new ErrorResponse
            {
                Error = "InternalServerError",
                Message = "An error occurred while promoting batch",
                Timestamp = DateTime.UtcNow,
                CorrelationId = HttpContext.TraceIdentifier
            });
        }
    }

    /// <summary>
    /// Trigger cleanup of snapshots older than 90 days (FR-RET-001)
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Number of snapshots deleted</returns>
    /// <remarks>
    /// Deletes all RateSnapshot records with snapshot_date older than 90 days.
    /// This endpoint is typically called by scheduled jobs, but can be triggered manually.
    /// </remarks>
    [HttpPost("cleanup")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> CleanupOldSnapshots(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("POST /v1/admin/snapshots/cleanup");

            var deletedCount = await _snapshotService.CleanupOldSnapshotsAsync(cancellationToken);

            Response.Headers["X-Correlation-ID"] = HttpContext.TraceIdentifier;

            return Ok(new
            {
                deletedCount,
                message = $"Deleted {deletedCount} snapshots older than 90 days",
                timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during snapshot cleanup");

            return StatusCode(StatusCodes.Status500InternalServerError, new ErrorResponse
            {
                Error = "InternalServerError",
                Message = "An error occurred during snapshot cleanup",
                Timestamp = DateTime.UtcNow,
                CorrelationId = HttpContext.TraceIdentifier
            });
        }
    }

    /// <summary>
    /// Get batch status
    /// </summary>
    /// <param name="batchId">Batch ID</param>
    /// <returns>Batch status</returns>
    [HttpGet("{batchId}/status")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ApiExplorerSettings(IgnoreApi = true)] // Hide from API documentation
    public IActionResult GetBatchStatus(string batchId)
    {
        var (status, error) = _snapshotQueue.GetStatus(batchId);
        
        return Ok(new
        {
            BatchId = batchId,
            Status = status,
            ErrorMessage = error
        });
    }
    /// <summary>
    /// Get batch audit log (FR-032)
    /// </summary>
    /// <param name="batchId">Batch ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Audit log</returns>
    [HttpGet("{batchId}/audit")]
    [ProducesResponseType(typeof(SnapshotAuditLog), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetBatchAudit(string batchId, CancellationToken cancellationToken = default)
    {
        try
        {
            var auditLog = await _snapshotService.GetBatchAuditAsync(batchId, cancellationToken);
            
            if (auditLog == null)
            {
                return NotFound(new ErrorResponse
                {
                    Error = "NotFound",
                    Message = $"Audit log for batch {batchId} not found",
                    Timestamp = DateTime.UtcNow,
                    CorrelationId = HttpContext.TraceIdentifier
                });
            }

            return Ok(auditLog);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving audit log for batch {BatchId}", batchId);
            return StatusCode(StatusCodes.Status500InternalServerError, new ErrorResponse
            {
                Error = "InternalServerError",
                Message = "An error occurred while retrieving audit log",
                Timestamp = DateTime.UtcNow,
                CorrelationId = HttpContext.TraceIdentifier
            });
        }
    }
}
