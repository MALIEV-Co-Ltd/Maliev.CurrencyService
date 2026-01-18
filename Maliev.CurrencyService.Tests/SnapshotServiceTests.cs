using Maliev.Aspire.ServiceDefaults.Caching;
using Maliev.CurrencyService.Api.Metrics;
using Maliev.CurrencyService.Api.Models.Snapshots;
using Maliev.CurrencyService.Api.Services;
using Maliev.CurrencyService.Data;
using Maliev.CurrencyService.Data.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Maliev.CurrencyService.Tests;

public class SnapshotServiceTests
{
    private readonly Mock<ICacheService> _cacheServiceMock;
    private readonly Mock<ILogger<SnapshotService>> _loggerMock;
    private readonly CurrencyServiceMetrics _metrics;
    private readonly CurrencyDbContext _context;

    public SnapshotServiceTests()
    {
        var options = new DbContextOptionsBuilder<CurrencyDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _context = new CurrencyDbContext(options);

        var configMock = new Mock<IConfiguration>();
        configMock.Setup(c => c["ASPNETCORE_ENVIRONMENT"]).Returns("Test");
        _metrics = new CurrencyServiceMetrics(configMock.Object);
        _cacheServiceMock = new Mock<ICacheService>();
        _loggerMock = new Mock<ILogger<SnapshotService>>();
    }

    [Fact]
    public async Task ImportBatchAsync_StagesValidSnapshots()
    {
        _context.Currencies.Add(new Currency { Id = Guid.NewGuid(), Code = "USD", Symbol = "$", Name = "US Dollar", DecimalPlaces = 2, IsActive = true });
        _context.Currencies.Add(new Currency { Id = Guid.NewGuid(), Code = "EUR", Symbol = "€", Name = "Euro", DecimalPlaces = 2, IsActive = true });
        await _context.SaveChangesAsync();

        var request = new SnapshotBatchRequest
        {
            Source = "Test",
            SnapshotDate = new DateOnly(2024, 1, 1),
            Snapshots = new List<SnapshotEntry>
            {
                new() { From = "USD", To = "EUR", Rate = 0.85m }
            }
        };

        var service = new SnapshotService(_context, _cacheServiceMock.Object, _loggerMock.Object, _metrics);
        var result = await service.ImportBatchAsync(request);

        Assert.Equal(1, result.SuccessCount);
        Assert.Equal(0, result.FailureCount);
    }

    [Fact]
    public async Task PromoteBatchAsync_MovesToProduction()
    {
        var batchId = Guid.NewGuid();
        _context.StagedSnapshots.Add(new StagedSnapshot
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
        await _context.SaveChangesAsync();

        var service = new SnapshotService(_context, _cacheServiceMock.Object, _loggerMock.Object, _metrics);
        var success = await service.PromoteBatchAsync(batchId.ToString(), "Manual");

        Assert.True(success);
        var production = await _context.RateSnapshots.FirstOrDefaultAsync(s => s.BatchId == batchId);
        Assert.NotNull(production);
        var stagedCount = await _context.StagedSnapshots.CountAsync();
        Assert.Equal(0, stagedCount);
    }

    [Fact]
    public async Task GetBatchAuditAsync_ReturnsAuditLog()
    {
        var batchId = Guid.NewGuid();
        _context.RateSnapshots.Add(new RateSnapshot
        {
            Id = Guid.NewGuid(),
            BatchId = batchId,
            FromCurrency = "USD",
            ToCurrency = "EUR",
            Rate = 0.85m,
            SnapshotDate = new DateOnly(2024, 1, 1),
            Source = "Manual",
            CreatedAt = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();

        var service = new SnapshotService(_context, _cacheServiceMock.Object, _loggerMock.Object, _metrics);
        var audit = await service.GetBatchAuditAsync(batchId.ToString());

        Assert.NotNull(audit);
        Assert.Equal(1, audit.RecordCount);
        Assert.Equal("Manual", audit.Source);
    }
}
