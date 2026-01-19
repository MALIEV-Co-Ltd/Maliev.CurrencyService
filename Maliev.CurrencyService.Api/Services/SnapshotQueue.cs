using Maliev.CurrencyService.Data;
using Maliev.CurrencyService.Data.Models;
using Microsoft.EntityFrameworkCore;
using System.Threading.Channels;

namespace Maliev.CurrencyService.Api.Services;

/// <summary>
/// In-memory queue for snapshot batch processing using System.Threading.Channels
/// </summary>
public class SnapshotQueue : ISnapshotQueue
{
    private readonly Channel<string> _queue;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<SnapshotQueue> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SnapshotQueue"/> class.
    /// </summary>
    public SnapshotQueue(IServiceProvider serviceProvider, ILogger<SnapshotQueue> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;

        var options = new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        };
        _queue = Channel.CreateUnbounded<string>(options);
    }

    /// <inheritdoc />
    public async ValueTask QueueBackgroundWorkItemAsync(string batchId)
    {
        if (string.IsNullOrEmpty(batchId))
        {
            throw new ArgumentNullException(nameof(batchId));
        }

        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<CurrencyDbContext>();

        var status = new BatchStatus
        {
            Id = Guid.NewGuid(),
            BatchId = batchId,
            Status = "Queued",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        context.BatchStatuses.Add(status);
        await context.SaveChangesAsync();

        await _queue.Writer.WriteAsync(batchId);
    }

    /// <inheritdoc />
    public async ValueTask<string> DequeueAsync(CancellationToken cancellationToken)
    {
        return await _queue.Reader.ReadAsync(cancellationToken);
    }

    /// <inheritdoc />
    public void UpdateStatus(string batchId, string status, string? error = null)
    {
        // Fire and forget update to DB (best effort in background)
        _ = Task.Run(async () =>
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<CurrencyDbContext>();

                var batchStatus = await context.BatchStatuses
                    .FirstOrDefaultAsync(s => s.BatchId == batchId);

                if (batchStatus != null)
                {
                    batchStatus.Status = status;
                    batchStatus.ErrorMessage = error;
                    batchStatus.UpdatedAt = DateTime.UtcNow;
                    await context.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update batch status for {BatchId} in background", batchId);
            }
        });
    }

    /// <inheritdoc />
    public (string Status, string? Error) GetStatus(string batchId)
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<CurrencyDbContext>();

        var batchStatus = context.BatchStatuses
            .AsNoTracking()
            .FirstOrDefault(s => s.BatchId == batchId);

        return batchStatus != null
            ? (batchStatus.Status, batchStatus.ErrorMessage)
            : ("NotFound", null);
    }
}
