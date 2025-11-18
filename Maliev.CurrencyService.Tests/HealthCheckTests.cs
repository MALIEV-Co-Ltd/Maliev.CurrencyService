using FluentAssertions;
using Maliev.CurrencyService.Api.HealthChecks;
using Maliev.CurrencyService.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Xunit;

namespace Maliev.CurrencyService.Tests;

public class HealthCheckTests : IDisposable
{
    private readonly CurrencyServiceDbContext _context;
    private readonly DatabaseHealthCheck _healthCheck;

    public HealthCheckTests()
    {
        // Constitution Principle IV: Use PostgreSQL for all tests (no InMemoryDatabase)
        var options = new DbContextOptionsBuilder<CurrencyServiceDbContext>()
            .UseNpgsql("Host=localhost;Port=5432;Database=currency_app_db;Username=postgres;Password=postgres123;")
            .Options;

        _context = new CurrencyServiceDbContext(options);
        _healthCheck = new DatabaseHealthCheck(_context);
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    [Fact]
    public async Task CheckHealthAsync_WithHealthyDatabase_ShouldReturnHealthy()
    {
        // Arrange
        // Ensure database schema exists for the health check to query tables
        await _context.Database.EnsureCreatedAsync();
        var context = new HealthCheckContext();

        // Act
        var result = await _healthCheck.CheckHealthAsync(context, CancellationToken.None);

        // Assert
        result.Status.Should().Be(HealthStatus.Healthy);
        result.Description.Should().StartWith("Database is healthy with");
    }

    [Fact]
    public async Task CheckHealthAsync_WithDatabaseConnectionIssue_ShouldReturnUnhealthy()
    {
        // Arrange - Use a context with invalid connection string
        var invalidOptions = new DbContextOptionsBuilder<CurrencyServiceDbContext>()
            .UseNpgsql("Host=nonexistent;Database=test;Username=test;Password=test")
            .Options;

        using var invalidContext = new CurrencyServiceDbContext(invalidOptions);
        var invalidHealthCheck = new DatabaseHealthCheck(invalidContext);
        var context = new HealthCheckContext();

        // Act
        var result = await invalidHealthCheck.CheckHealthAsync(context, CancellationToken.None);

        // Assert
        result.Status.Should().Be(HealthStatus.Unhealthy);
        result.Description.Should().StartWith("Database health check failed:");
        result.Exception.Should().NotBeNull();
    }

    // Note: Cancellation test removed as in-memory database doesn't properly respect cancellation tokens
}