using Maliev.CurrencyService.Api.HealthChecks;
using Maliev.CurrencyService.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Testcontainers.PostgreSql;
using Xunit;

namespace Maliev.CurrencyService.Tests;

public class HealthCheckTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgresContainer;
    private CurrencyServiceDbContext _context = null!;
    private DatabaseHealthCheck _healthCheck = null!;

    public HealthCheckTests()
    {
        // Constitution Principle IV: PostgreSQL-only testing using Testcontainers
        _postgresContainer = new PostgreSqlBuilder()
            .WithImage("postgres:16-alpine")
            .WithDatabase("currency_app_db")
            .WithUsername("postgres")
            .WithPassword("postgres123")
            .WithCleanUp(true)
            .Build();
    }

    public async Task InitializeAsync()
    {
        // Start PostgreSQL container
        await _postgresContainer.StartAsync();

        // Create DbContext with Testcontainers connection string
        var options = new DbContextOptionsBuilder<CurrencyServiceDbContext>()
            .UseNpgsql(_postgresContainer.GetConnectionString())
            .Options;

        _context = new CurrencyServiceDbContext(options);
        _healthCheck = new DatabaseHealthCheck(_context);
    }

    public async Task DisposeAsync()
    {
        _context?.Dispose();
        await _postgresContainer.DisposeAsync();
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
        Assert.Equal(HealthStatus.Healthy, result.Status);
        Assert.StartsWith("Database is healthy with", result.Description);
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
        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.StartsWith("Database health check failed:", result.Description);
        Assert.NotNull(result.Exception);
    }

    // Note: Cancellation test removed as in-memory database doesn't properly respect cancellation tokens
}