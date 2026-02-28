using Maliev.CurrencyService.Domain.Entities;
using Maliev.CurrencyService.Domain.Interfaces;
using Maliev.CurrencyService.Infrastructure.Persistence;
using Maliev.CurrencyService.Infrastructure.Persistence.Interceptors;
using Maliev.CurrencyService.Tests.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Maliev.CurrencyService.Tests;

public class InterceptorIntegrationTests : IClassFixture<BaseIntegrationTestFactory<Program, CurrencyDbContext>>
{
    private readonly BaseIntegrationTestFactory<Program, CurrencyDbContext> _factory;
    private readonly Mock<IDatabaseMetrics> _databaseMetricsMock = new();
    private readonly Mock<ILogger<AuditLogInterceptor>> _auditLoggerMock = new();

    public InterceptorIntegrationTests(BaseIntegrationTestFactory<Program, CurrencyDbContext> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Interceptors_AreTriggered_InIntegration()
    {
        // Arrange
        var customFactory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.AddSingleton(_databaseMetricsMock.Object);
                services.AddSingleton(_auditLoggerMock.Object);
            });
        });

        using var scope = customFactory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<CurrencyDbContext>();

        var currency = new Currency
        {
            Id = Guid.NewGuid(),
            Code = "IT2",
            Name = "Integration Test 2",
            Symbol = "I",
            DecimalPlaces = 2,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        // Act
        context.Currencies.Add(currency);
        await context.SaveChangesAsync();

        // Assert Audit
        _auditLoggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Audit: Added on Currency")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);

        // Act - Query
        await context.Currencies.ToListAsync();

        // Assert Metrics
        _databaseMetricsMock.Verify(m => m.RecordDatabaseQuery("SELECT"), Times.AtLeastOnce);
    }

    [Fact]
    public async Task DatabaseMetricsInterceptor_RecordsError_OnFailure()
    {
        // Arrange
        var customFactory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.AddSingleton(_databaseMetricsMock.Object);
            });
        });

        using var scope = customFactory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<CurrencyDbContext>();

        // Act & Assert
        // Force an error by executing raw SQL that is invalid
        await Assert.ThrowsAnyAsync<Exception>(() => context.Database.ExecuteSqlRawAsync("SELECT * FROM non_existent_table"));

        // Verify RecordDatabaseError was called
        _databaseMetricsMock.Verify(m => m.RecordDatabaseError(It.IsAny<string>(), It.IsAny<string>()), Times.AtLeastOnce);
    }
}
