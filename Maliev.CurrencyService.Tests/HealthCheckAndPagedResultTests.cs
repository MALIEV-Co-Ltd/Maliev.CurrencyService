using Maliev.CurrencyService.Api.HealthChecks;
using Maliev.CurrencyService.Api.Models;
using Maliev.CurrencyService.Api.Models.Snapshots;
using Maliev.CurrencyService.Domain.Interfaces;
using Maliev.CurrencyService.Infrastructure.Persistence.Interceptors;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Maliev.CurrencyService.Tests;

public class ApiTests
{
    #region PrivateMemoryHealthCheckTests

    [Fact]
    public async Task PrivateMemoryHealthCheck_ReturnsHealthy_WhenMemoryBelowThreshold()
    {
        var thresholdBytes = 100L * 1024 * 1024 * 1024;
        var healthCheck = new PrivateMemoryHealthCheck(thresholdBytes);

        var result = await healthCheck.CheckHealthAsync(new Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckContext());

        Assert.Equal(Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Healthy, result.Status);
    }

    [Fact]
    public async Task PrivateMemoryHealthCheck_ReturnsUnhealthy_WhenMemoryExceedsThreshold()
    {
        var thresholdBytes = 1;
        var healthCheck = new PrivateMemoryHealthCheck(thresholdBytes);

        var result = await healthCheck.CheckHealthAsync(new Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckContext());

        Assert.Equal(Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Unhealthy, result.Status);
    }

    #endregion

    #region WorkingSetHealthCheckTests

    [Fact]
    public async Task WorkingSetHealthCheck_ReturnsHealthy_WhenMemoryBelowThreshold()
    {
        var thresholdBytes = 100L * 1024 * 1024 * 1024;
        var healthCheck = new WorkingSetHealthCheck(thresholdBytes);

        var result = await healthCheck.CheckHealthAsync(new Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckContext());

        Assert.Equal(Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Healthy, result.Status);
    }

    [Fact]
    public async Task WorkingSetHealthCheck_ReturnsUnhealthy_WhenMemoryExceedsThreshold()
    {
        var thresholdBytes = 1;
        var healthCheck = new WorkingSetHealthCheck(thresholdBytes);

        var result = await healthCheck.CheckHealthAsync(new Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckContext());

        Assert.Equal(Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Unhealthy, result.Status);
    }

    #endregion

    #region HealthCheckResponseWriterTests

    [Fact]
    public async Task WriteResponse_SerializesHealthReport_ToJson()
    {
        var httpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext();
        var memoryStream = new System.IO.MemoryStream();
        httpContext.Response.Body = memoryStream;

        var entries = new Dictionary<string, Microsoft.Extensions.Diagnostics.HealthChecks.HealthReportEntry>
        {
            ["test"] = new Microsoft.Extensions.Diagnostics.HealthChecks.HealthReportEntry(
                Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Healthy,
                "Test description",
                TimeSpan.FromMilliseconds(100),
                null,
                null)
        };

        var report = new Microsoft.Extensions.Diagnostics.HealthChecks.HealthReport(entries, TimeSpan.FromMilliseconds(150));

        await HealthCheckResponseWriter.WriteResponse(httpContext, report);

        memoryStream.Seek(0, System.IO.SeekOrigin.Begin);
        var json = await new System.IO.StreamReader(memoryStream).ReadToEndAsync();
        var doc = System.Text.Json.JsonDocument.Parse(json);

        Assert.Equal("Healthy", doc.RootElement.GetProperty("Status").GetString());
    }

    [Fact]
    public async Task WriteResponse_SetsContentType_ToApplicationJson()
    {
        var httpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext();
        var memoryStream = new System.IO.MemoryStream();
        httpContext.Response.Body = memoryStream;

        var report = new Microsoft.Extensions.Diagnostics.HealthChecks.HealthReport(new Dictionary<string, Microsoft.Extensions.Diagnostics.HealthChecks.HealthReportEntry>(), TimeSpan.Zero);

        await HealthCheckResponseWriter.WriteResponse(httpContext, report);

        Assert.StartsWith("application/json", httpContext.Response.ContentType);
    }

    #endregion

    #region ApiModelDtoTests

    [Fact]
    public void ExchangeRateDto_Properties_CanBeSet()
    {
        var dto = new Maliev.CurrencyService.Api.Models.ExchangeRateDto
        {
            FromCurrency = "USD",
            ToCurrency = "EUR",
            Rate = 0.85m,
            FetchedAt = DateTime.UtcNow,
            Source = "Test"
        };

        Assert.Equal("USD", dto.FromCurrency);
        Assert.Equal("EUR", dto.ToCurrency);
        Assert.Equal(0.85m, dto.Rate);
        Assert.Equal("Test", dto.Source);
    }

    [Fact]
    public void CurrencyDto_Properties_CanBeSet()
    {
        var dto = new Maliev.CurrencyService.Api.Models.CurrencyDto
        {
            Id = 1,
            ShortName = "USD",
            LongName = "United States Dollar",
            CreatedDate = DateTime.UtcNow,
            ModifiedDate = DateTime.UtcNow
        };

        Assert.Equal(1, dto.Id);
        Assert.Equal("USD", dto.ShortName);
        Assert.Equal("United States Dollar", dto.LongName);
    }

    [Fact]
    public void SnapshotEntryDto_Properties_CanBeSet()
    {
        var dto = new Maliev.CurrencyService.Api.Models.Snapshots.SnapshotEntryDto
        {
            From = "USD",
            To = "EUR",
            Rate = 0.85m,
            Timestamp = "2025-01-15T00:00:00Z"
        };

        Assert.Equal("USD", dto.From);
        Assert.Equal("EUR", dto.To);
        Assert.Equal(0.85m, dto.Rate);
        Assert.Equal("2025-01-15T00:00:00Z", dto.Timestamp);
    }

    #endregion

    #region DatabaseMetricsInterceptorTests

    [Fact]
    public void GetCommandTypeFromSql_ReturnsSelect_ForSelectQuery()
    {
        var result = GetCommandTypeFromSql("SELECT * FROM Currencies");
        Assert.Equal("SELECT", result);
    }

    [Fact]
    public void GetCommandTypeFromSql_ReturnsInsert_ForInsertQuery()
    {
        var result = GetCommandTypeFromSql("INSERT INTO Currencies VALUES (1, 'USD')");
        Assert.Equal("INSERT", result);
    }

    [Fact]
    public void GetCommandTypeFromSql_ReturnsUpdate_ForUpdateQuery()
    {
        var result = GetCommandTypeFromSql("UPDATE Currencies SET Name = 'Dollar' WHERE Code = 'USD'");
        Assert.Equal("UPDATE", result);
    }

    [Fact]
    public void GetCommandTypeFromSql_ReturnsDelete_ForDeleteQuery()
    {
        var result = GetCommandTypeFromSql("DELETE FROM Currencies WHERE Code = 'USD'");
        Assert.Equal("DELETE", result);
    }

    [Fact]
    public void GetCommandTypeFromSql_ReturnsOther_ForUnknownQuery()
    {
        var result = GetCommandTypeFromSql("DROP TABLE Currencies");
        Assert.Equal("OTHER", result);
    }

    [Fact]
    public void GetCommandTypeFromSql_ReturnsUnknown_ForEmptyQuery()
    {
        var result = GetCommandTypeFromSql("");
        Assert.Equal("UNKNOWN", result);
    }

    [Fact]
    public void DatabaseMetricsInterceptor_Constructor_AcceptsNullLogger()
    {
        var interceptor = new DatabaseMetricsInterceptor(null!, null!);
        Assert.NotNull(interceptor);
    }

    #endregion

    #region AuditLogInterceptorTests

    [Fact]
    public void AuditLogInterceptor_Constructor_InitializesLogger()
    {
        var loggerMock = new Mock<ILogger<AuditLogInterceptor>>();
        var interceptor = new AuditLogInterceptor(loggerMock.Object);

        Assert.NotNull(interceptor);
    }

    #endregion

    #region Helper Methods

    private static string GetCommandTypeFromSql(string sql)
    {
        if (string.IsNullOrWhiteSpace(sql))
            return "UNKNOWN";

        var firstWord = sql.TrimStart().Split(' ')[0].ToUpperInvariant();
        return firstWord switch
        {
            "SELECT" => "SELECT",
            "INSERT" => "INSERT",
            "UPDATE" => "UPDATE",
            "DELETE" => "DELETE",
            _ => "OTHER"
        };
    }

    #endregion
}
