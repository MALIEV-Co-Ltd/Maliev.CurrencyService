using Maliev.CurrencyService.Api.Metrics;
using Maliev.CurrencyService.Api.Models.Snapshots;
using Maliev.CurrencyService.Data;
using Maliev.CurrencyService.Data.Models;
using Microsoft.EntityFrameworkCore;
using Maliev.Aspire.ServiceDefaults.Caching;

namespace Maliev.CurrencyService.Api.Services;

/// <summary>
/// Service for managing exchange rate snapshot batch ingestion
/// </summary>
/// <remarks>
/// User Story 4: Implements staging table pattern for bulk import validation,
/// promotion to production, and 90-day retention cleanup.
/// </remarks>
public class SnapshotService : ISnapshotService
{
    private readonly CurrencyDbContext _context;
    private readonly ICacheService _cacheService;
    private readonly ILogger<SnapshotService> _logger;
    private readonly CurrencyServiceMetrics _metrics;

    private const int MaxRetentionDays = 90; // FR-RET-001

    /// <summary>
    /// Initializes a new instance of the <see cref="SnapshotService"/> class.
    /// </summary>
    /// <param name="context">The database context.</param>
    /// <param name="cacheService">The cache service.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="metrics">The metrics service.</param>
    public SnapshotService(
        CurrencyDbContext context,
        ICacheService cacheService,
        ILogger<SnapshotService> logger,
        CurrencyServiceMetrics metrics)
    {
        _context = context;
        _cacheService = cacheService;
        _logger = logger;
        _metrics = metrics;
    }

    /// <summary>
    /// Imports a batch of snapshots, optionally promoting directly to production.
    /// </summary>
    /// <param name="request">The batch request containing snapshots.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A <see cref="SnapshotBatchResponse"/> detailing the import operation result.</returns>
    public async Task<SnapshotBatchResponse> ImportBatchAsync(
        SnapshotBatchRequest request,
        CancellationToken cancellationToken = default)
    {
        var batchId = Guid.NewGuid();
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        _logger.LogInformation("Starting batch import {BatchId}: {Count} snapshots for {Date} from {Source}",
            batchId, request.Snapshots.Count, request.SnapshotDate, request.Source);

        // Load all valid currency codes for validation
        var validCurrencies = await _context.Currencies
            .Where(c => c.IsActive)
            .Select(c => c.Code)
            .ToHashSetAsync(cancellationToken);

        var successCount = 0;
        var failureCount = 0;
        var errors = new Dictionary<string, List<string>>();
        var stagedSnapshots = new List<StagedSnapshot>();

        // Process each snapshot entry
        foreach (var entry in request.Snapshots)
        {
            var from = entry.From.ToUpperInvariant();
            var to = entry.To.ToUpperInvariant();
            var pairKey = $"{from}:{to}";

            try
            {
                // Validate currencies exist and are active
                if (!validCurrencies.Contains(from))
                {
                    AddError(errors, pairKey, $"From currency '{from}' not found or inactive");
                    failureCount++;
                    continue;
                }

                if (!validCurrencies.Contains(to))
                {
                    AddError(errors, pairKey, $"To currency '{to}' not found or inactive");
                    failureCount++;
                    continue;
                }

                // Create staged snapshot
                var stagedSnapshot = new StagedSnapshot
                {
                    Id = Guid.NewGuid(),
                    BatchId = batchId,
                    FromCurrency = from,
                    ToCurrency = to,
                    Rate = entry.Rate,
                    SnapshotDate = request.SnapshotDate,
                    Status = "Validated",
                    ValidationError = null,
                    CreatedAt = DateTime.UtcNow
                };

                stagedSnapshots.Add(stagedSnapshot);
                successCount++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating snapshot {Pair}", pairKey);
                AddError(errors, pairKey, $"Validation error: {ex.Message}");
                failureCount++;
            }
        }

        // Save staged snapshots in bulk
        if (stagedSnapshots.Any())
        {
            _context.StagedSnapshots.AddRange(stagedSnapshots);
            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Staged {Count} snapshots for batch {BatchId}", stagedSnapshots.Count, batchId);
        }

        stopwatch.Stop();
        _metrics.RecordBackgroundJobDuration("snapshot_import", stopwatch.Elapsed.TotalSeconds);

        // Auto-promote if requested
        var status = "staged";
        if (request.AutoPromote && stagedSnapshots.Any())
        {
            _logger.LogInformation("Auto-promoting batch {BatchId}", batchId);
            var promoted = await PromoteBatchAsync(batchId.ToString(), request.Source, cancellationToken);
            status = promoted ? "promoted" : "partial";
        }

        var response = new SnapshotBatchResponse
        {
            BatchId = batchId.ToString(),
            SnapshotDate = request.SnapshotDate,
            Source = request.Source,
            SuccessCount = successCount,
            FailureCount = failureCount,
            Status = status,
            Errors = errors.Any() ? errors.ToDictionary(x => x.Key, x => x.Value.ToArray()) : null,
            ProcessedAt = DateTime.UtcNow
        };

        _logger.LogInformation("Batch import {BatchId} completed: {Success} success, {Failure} failed, {Status} status, {Elapsed}ms",
            batchId, successCount, failureCount, status, stopwatch.ElapsedMilliseconds);

        return response;
    }

    /// <summary>
    /// Promotes staged snapshots to production.
    /// </summary>
    /// <param name="batchId">The unique identifier of the batch to promote.</param>
    /// <param name="source">Optional source/provider name for the snapshots.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>True if promotion succeeded, otherwise false.</returns>
    public async Task<bool> PromoteBatchAsync(
        string batchId,
        string? source = null,
        CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(batchId, out var parsedBatchId))
        {
            _logger.LogWarning("Invalid batch ID format: {BatchId}", batchId);
            return false;
        }

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        _logger.LogInformation("Promoting batch {BatchId} to production", batchId);

        try
        {
            // Get all validated staged snapshots for this batch
            var stagedSnapshots = await _context.StagedSnapshots
                .Where(s => s.BatchId == parsedBatchId && s.Status == "Validated")
                .ToListAsync(cancellationToken);

            if (!stagedSnapshots.Any())
            {
                _logger.LogWarning("No validated snapshots found for batch {BatchId}", batchId);
                return false;
            }

            var affectedPairs = new HashSet<string>();

            // Convert to production snapshots
            var productionSnapshots = stagedSnapshots.Select(staged =>
            {
                affectedPairs.Add($"{staged.FromCurrency}:{staged.ToCurrency}");

                return new RateSnapshot
                {
                    Id = Guid.NewGuid(),
                    BatchId = staged.BatchId,
                    FromCurrency = staged.FromCurrency,
                    ToCurrency = staged.ToCurrency,
                    Rate = staged.Rate,
                    SnapshotDate = staged.SnapshotDate,
                    Source = source,
                    CreatedAt = DateTime.UtcNow
                };
            }).ToList();

            // Upsert: Delete existing snapshots for same date/pairs, then insert new ones
            var snapshotDate = stagedSnapshots.First().SnapshotDate;
            var fromCodes = stagedSnapshots.Select(p => p.FromCurrency).Distinct().ToList();
            var toCodes = stagedSnapshots.Select(p => p.ToCurrency).Distinct().ToList();

            var existingToDrop = await _context.RateSnapshots
                .Where(r => r.SnapshotDate == snapshotDate && fromCodes.Contains(r.FromCurrency) && toCodes.Contains(r.ToCurrency))
                .ToListAsync(cancellationToken);

            var snapshotsToRemove = existingToDrop
                .Where(r => stagedSnapshots.Any(p => p.FromCurrency == r.FromCurrency && p.ToCurrency == r.ToCurrency))
                .ToList();

            if (snapshotsToRemove.Any())
            {
                _context.RateSnapshots.RemoveRange(snapshotsToRemove);
                _logger.LogInformation("Removed {Count} existing snapshots for promotion of batch {BatchId}",
                    snapshotsToRemove.Count, batchId);
            }

            // Add new production snapshots
            _context.RateSnapshots.AddRange(productionSnapshots);

            // Remove staged snapshots
            _context.StagedSnapshots.RemoveRange(stagedSnapshots);

            await _context.SaveChangesAsync(cancellationToken);

            // Invalidate cache for affected pairs
            await InvalidateCacheForPairsAsync(affectedPairs, snapshotDate, cancellationToken);

            stopwatch.Stop();
            _metrics.RecordBackgroundJobDuration("snapshot_promotion", stopwatch.Elapsed.TotalSeconds);

            _logger.LogInformation("Promoted batch {BatchId}: {Count} snapshots in {Elapsed}ms, invalidated {CacheCount} cache keys",
                batchId, productionSnapshots.Count, stopwatch.ElapsedMilliseconds, affectedPairs.Count);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error promoting batch {BatchId}", batchId);
            _metrics.RecordBackgroundJobFailure("snapshot_promotion", "exception");
            return false;
        }
    }

    /// <summary>
    /// Cleans up snapshots older than 90 days.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The number of snapshots deleted.</returns>
    public async Task<int> CleanupOldSnapshotsAsync(CancellationToken cancellationToken = default)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var cutoffDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-MaxRetentionDays));

        _logger.LogInformation("Starting snapshot cleanup: deleting snapshots older than {CutoffDate}", cutoffDate);

        try
        {
            // Delete old snapshots
            var oldSnapshots = await _context.RateSnapshots
                .Where(r => r.SnapshotDate < cutoffDate)
                .ToListAsync(cancellationToken);

            if (oldSnapshots.Any())
            {
                _context.RateSnapshots.RemoveRange(oldSnapshots);
                await _context.SaveChangesAsync(cancellationToken);
            }

            stopwatch.Stop();
            _metrics.RecordBackgroundJobDuration("snapshot_cleanup", stopwatch.Elapsed.TotalSeconds);

            _logger.LogInformation("Snapshot cleanup completed: deleted {Count} snapshots in {Elapsed}ms",
                oldSnapshots.Count, stopwatch.ElapsedMilliseconds);

            return oldSnapshots.Count;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during snapshot cleanup");
            _metrics.RecordBackgroundJobFailure("snapshot_cleanup", "exception");
            return 0;
        }
    }

    /// <summary>
    /// Invalidates cache keys for affected currency pairs
    /// </summary>
    private async Task InvalidateCacheForPairsAsync(
        HashSet<string> pairs,
        DateOnly snapshotDate,
        CancellationToken cancellationToken)
    {
        foreach (var pair in pairs)
        {
            var parts = pair.Split(':');
            if (parts.Length == 2)
            {
                var cacheKey = $"snapshot:{parts[0]}:{parts[1]}:{snapshotDate:yyyy-MM-dd}";
                await _cacheService.RemoveAsync(cacheKey, cancellationToken);
                _logger.LogDebug("Invalidated cache key: {CacheKey}", cacheKey);
            }
        }
    }

    /// <summary>
    /// Retrieves audit log for a specific batch (FR-032)
    /// </summary>
    public async Task<SnapshotAuditLog?> GetBatchAuditAsync(string batchId, CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(batchId, out var parsedBatchId))
        {
            return null;
        }

        // 1. Check RateSnapshots (Promoted/History)
        // Optimistic: Only need one entry to get metadata, but need count.
        // For accurate count, we must count.
        var promotedCount = await _context.RateSnapshots
            .Where(r => r.BatchId == parsedBatchId)
            .CountAsync(cancellationToken);

        if (promotedCount > 0)
        {
            var first = await _context.RateSnapshots
                .Where(r => r.BatchId == parsedBatchId)
                .Select(r => new { r.CreatedAt, r.Source })
                .FirstAsync(cancellationToken);

            return new SnapshotAuditLog
            {
                BatchId = batchId,
                Timestamp = first.CreatedAt,
                RecordCount = promotedCount,
                Source = first.Source ?? "Unknown",
                SubmittedBy = "Admin" // Placeholder as not currently tracked in schema
            };
        }

        // 2. Check StagedSnapshots (Pending/Validation)
        var stagedCount = await _context.StagedSnapshots
            .Where(s => s.BatchId == parsedBatchId)
            .CountAsync(cancellationToken);

        if (stagedCount > 0)
        {
            var first = await _context.StagedSnapshots
                .Where(s => s.BatchId == parsedBatchId)
                .Select(s => new { s.CreatedAt })
                .FirstAsync(cancellationToken);

            return new SnapshotAuditLog
            {
                BatchId = batchId,
                Timestamp = first.CreatedAt,
                RecordCount = stagedCount,
                Source = "Staging", // Source not stored in StagedSnapshot
                SubmittedBy = "Admin"
            };
        }

        return null;
    }

    /// <summary>
    /// Adds validation error to dictionary
    /// </summary>
    private static void AddError(Dictionary<string, List<string>> errors, string key, string message)
    {
        if (!errors.ContainsKey(key))
        {
            errors[key] = new List<string>();
        }
        errors[key].Add(message);
    }
}
