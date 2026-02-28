using Maliev.CurrencyService.Infrastructure.Persistence;
using Maliev.CurrencyService.Tests.Testing;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Maliev.CurrencyService.Tests.Infrastructure;

/// <summary>
/// Verifies EF Core model integrity using a real PostgreSQL connection via Testcontainers.
/// </summary>
public class ModelIntegrityTests : IClassFixture<BaseIntegrationTestFactory<Program, CurrencyDbContext>>
{
    private readonly BaseIntegrationTestFactory<Program, CurrencyDbContext> _factory;

    /// <summary>Initializes a new instance of the <see cref="ModelIntegrityTests"/> class.</summary>
    public ModelIntegrityTests(BaseIntegrationTestFactory<Program, CurrencyDbContext> factory)
    {
        _factory = factory;
    }

    /// <summary>Database should have no pending EF Core migrations after startup.</summary>
    [Fact]
    public async Task Model_ShouldNotHavePendingMigrations()
    {
        using var context = _factory.CreateDbContext();
        var pendingMigrations = await context.Database.GetPendingMigrationsAsync();

        Assert.Empty(pendingMigrations);
    }
}
