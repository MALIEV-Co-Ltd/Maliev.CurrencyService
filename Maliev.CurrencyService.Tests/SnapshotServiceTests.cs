using Maliev.Aspire.ServiceDefaults.Caching;
using Maliev.CurrencyService.Api.Metrics;
using Maliev.CurrencyService.Application.DTOs.Snapshots;
using Maliev.CurrencyService.Application.Interfaces;
using Maliev.CurrencyService.Domain.Entities;
using Maliev.CurrencyService.Infrastructure.Persistence;
using Maliev.CurrencyService.Infrastructure.Services;
using Maliev.CurrencyService.Tests.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Maliev.CurrencyService.Tests;

/// <summary>
/// Unit tests for <see cref="SnapshotService"/> using real PostgreSQL via Testcontainers.
/// </summary>
public class SnapshotServiceTests : IClassFixture<BaseIntegrationTestFactory<Program, CurrencyDbContext>>, IAsyncLifetime
{
    private readonly BaseIntegrationTestFactory<Program, CurrencyDbContext> _factory;
    private readonly Mock<ICacheService> _cacheServiceMock;
    private readonly Mock<ILogger<SnapshotService>> _loggerMock;
    private readonly CurrencyServiceMetrics _metrics;
    private CurrencyDbContext _context = null!;

    /// <summary>Initializes a new instance of the <see cref="SnapshotServiceTests"/> class.</summary>
    public SnapshotServiceTests(BaseIntegrationTestFactory<Program, CurrencyDbContext> factory)
    {
        _factory = factory;

        var configMock = new Mock<IConfiguration>();
        configMock.Setup(c => c["ASPNETCORE_ENVIRONMENT"]).Returns("Test");
        _metrics = new CurrencyServiceMetrics(configMock.Object);
        _cacheServiceMock = new Mock<ICacheService>();
        _loggerMock = new Mock<ILogger<SnapshotService>>();
    }

    /// <inheritdoc/>
    public async Task InitializeAsync()
    {
        await _factory.CleanDatabaseAsync();
        _context = _factory.CreateDbContext();
    }

    /// <inheritdoc/>
    public async Task DisposeAsync()
    {
        await _context.DisposeAsync();
    }

    private SnapshotService CreateService() =>
        new SnapshotService(_context, _cacheServiceMock.Object, _loggerMock.Object, _metrics);

    /// <summary>ImportBatchAsync stages valid snapshots.</summary>
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

        var service = CreateService();
        var result = await service.ImportBatchAsync(request);

        Assert.Equal(1, result.SuccessCount);
        Assert.Equal(0, result.FailureCount);
    }

    /// <summary>PromoteBatchAsync moves staged snapshots to production.</summary>
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

        var service = CreateService();
        var success = await service.PromoteBatchAsync(batchId.ToString(), "Manual");

        Assert.True(success);
        var production = await _context.RateSnapshots.FirstOrDefaultAsync(s => s.BatchId == batchId);
        Assert.NotNull(production);
        var stagedCount = await _context.StagedSnapshots.CountAsync();
        Assert.Equal(0, stagedCount);
    }

    /// <summary>GetBatchAuditAsync returns the audit log for a batch.</summary>
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

        var service = CreateService();
        var audit = await service.GetBatchAuditAsync(batchId.ToString());

        Assert.NotNull(audit);
        Assert.Equal(1, audit.RecordCount);
        Assert.Equal("Manual", audit.Source);
    }
}
