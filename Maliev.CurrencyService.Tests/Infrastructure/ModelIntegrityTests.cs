using Maliev.CurrencyService.Data;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Maliev.CurrencyService.Tests.Infrastructure;

public class ModelIntegrityTests
{
    [Fact]
    public void Model_ShouldNotHavePendingChanges()
    {
        var options = new DbContextOptionsBuilder<CurrencyDbContext>()
            .UseNpgsql("Host=localhost;Database=ModelCheck")
            .Options;

        using var context = new CurrencyDbContext(options);
        var hasChanges = context.Database.HasPendingModelChanges();

        Assert.False(hasChanges, "Run 'dotnet ef migrations add <Name> --project Maliev.CurrencyService.Data --startup-project Maliev.CurrencyService.Api'");
    }
}
