using Maliev.CurrencyService.Api.Metrics;
using Maliev.CurrencyService.Api.Models.Snapshots;
using Maliev.CurrencyService.Data;
using Maliev.CurrencyService.Data.Models;
using Microsoft.EntityFrameworkCore;

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
    private readonly CurrencyServiceDbContext _context;
    private readonly ICacheService _cacheService;
    private readonly ILogger<SnapshotService> _logger;
    private readonly CurrencyServiceMetrics _metrics;

    private const int MaxRetentionDays = 90; // FR-RET-001

    public SnapshotService(
        CurrencyServiceDbContext context,
        ICacheService cacheService,
        ILogger<SnapshotService> logger,
        CurrencyServiceMetrics metrics)
    {
        _context = context;
        _cacheService = cacheService;
        _logger = logger;
        _metrics = metrics;
    }

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
        _metrics.BackgroundJobDuration.WithLabels("snapshot_import").Observe(stopwatch.Elapsed.TotalSeconds);

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
            var currencyPairs = stagedSnapshots.Select(s => new { s.FromCurrency, s.ToCurrency }).ToList();

            foreach (var pair in currencyPairs)
            {
                var existing = await _context.RateSnapshots
                    .Where(r => r.FromCurrency == pair.FromCurrency
                                && r.ToCurrency == pair.ToCurrency
                                && r.SnapshotDate == snapshotDate)
                    .ToListAsync(cancellationToken);

                if (existing.Any())
                {
                    _context.RateSnapshots.RemoveRange(existing);
                    _logger.LogDebug("Removed {Count} existing snapshots for {From}:{To} on {Date}",
                        existing.Count, pair.FromCurrency, pair.ToCurrency, snapshotDate);
                }
            }

            // Add new production snapshots
            _context.RateSnapshots.AddRange(productionSnapshots);

            // Remove staged snapshots
            _context.StagedSnapshots.RemoveRange(stagedSnapshots);

            await _context.SaveChangesAsync(cancellationToken);

            // Invalidate cache for affected pairs
            await InvalidateCacheForPairsAsync(affectedPairs, snapshotDate, cancellationToken);

            stopwatch.Stop();
            _metrics.BackgroundJobDuration.WithLabels("snapshot_promotion").Observe(stopwatch.Elapsed.TotalSeconds);

            _logger.LogInformation("Promoted batch {BatchId}: {Count} snapshots in {Elapsed}ms, invalidated {CacheCount} cache keys",
                batchId, productionSnapshots.Count, stopwatch.ElapsedMilliseconds, affectedPairs.Count);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error promoting batch {BatchId}", batchId);
            _metrics.BackgroundJobFailures.WithLabels("snapshot_promotion", "exception").Inc();
            return false;
        }
    }

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
            _metrics.BackgroundJobDuration.WithLabels("snapshot_cleanup").Observe(stopwatch.Elapsed.TotalSeconds);

            _logger.LogInformation("Snapshot cleanup completed: deleted {Count} snapshots in {Elapsed}ms",
                oldSnapshots.Count, stopwatch.ElapsedMilliseconds);

            return oldSnapshots.Count;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during snapshot cleanup");
            _metrics.BackgroundJobFailures.WithLabels("snapshot_cleanup", "exception").Inc();
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
