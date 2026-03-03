using Maliev.CurrencyService.Domain.Entities;
using Maliev.CurrencyService.Infrastructure.Persistence;
using Maliev.CurrencyService.Infrastructure.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Maliev.CurrencyService.Tests;

public class DomainTests
{
    private CurrencyDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<CurrencyDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new CurrencyDbContext(options);
    }

    #region Currency Entity Tests

    [Fact]
    public void Currency_DefaultConstructor_ShouldInitializeDefaultValues()
    {
        var currency = new Currency();

        Assert.Equal(string.Empty, currency.Code);
        Assert.Equal(string.Empty, currency.Symbol);
        Assert.Equal(string.Empty, currency.Name);
        Assert.Equal(2, currency.DecimalPlaces);
        Assert.True(currency.IsActive);
        Assert.False(currency.IsPrimary);
        Assert.NotNull(currency.Version);
        Assert.Equal(8, currency.Version.Length);
    }

    [Fact]
    public void Currency_SetProperties_ShouldSetCorrectValues()
    {
        var id = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var currency = new Currency
        {
            Id = id,
            Code = "USD",
            Symbol = "$",
            Name = "US Dollar",
            DecimalPlaces = 2,
            IsActive = true,
            IsPrimary = true,
            CreatedAt = now,
            UpdatedAt = now,
            Version = new byte[8]
        };

        Assert.Equal(id, currency.Id);
        Assert.Equal("USD", currency.Code);
        Assert.Equal("$", currency.Symbol);
        Assert.Equal("US Dollar", currency.Name);
        Assert.Equal(2, currency.DecimalPlaces);
        Assert.True(currency.IsActive);
        Assert.True(currency.IsPrimary);
        Assert.Equal(now, currency.CreatedAt);
        Assert.Equal(now, currency.UpdatedAt);
    }

    [Fact]
    public void Currency_HasRequiredAttributes()
    {
        var context = CreateInMemoryDbContext();
        var model = context.Model;
        var entityType = model.FindEntityType(typeof(Currency));

        Assert.NotNull(entityType);
        Assert.Equal("currencies", entityType.GetTableName());

        var idProperty = entityType.FindProperty(nameof(Currency.Id));
        Assert.NotNull(idProperty);
        Assert.True(idProperty.IsPrimaryKey());

        var codeProperty = entityType.FindProperty(nameof(Currency.Code));
        Assert.NotNull(codeProperty);
        Assert.False(codeProperty.IsNullable);
        Assert.Equal(3, codeProperty.GetMaxLength());

        var nameProperty = entityType.FindProperty(nameof(Currency.Name));
        Assert.NotNull(nameProperty);
        Assert.Equal(100, nameProperty.GetMaxLength());
    }

    #endregion

    #region ExchangeRate Entity Tests

    [Fact]
    public void ExchangeRate_DefaultConstructor_ShouldInitializeDefaultValues()
    {
        var exchangeRate = new ExchangeRate();

        Assert.Equal(string.Empty, exchangeRate.FromCurrency);
        Assert.Equal(string.Empty, exchangeRate.ToCurrency);
        Assert.Equal(0m, exchangeRate.Rate);
        Assert.Equal(string.Empty, exchangeRate.Provider);
        Assert.False(exchangeRate.IsTransitive);
        Assert.Null(exchangeRate.IntermediateCurrency);
    }

    [Fact]
    public void ExchangeRate_SetProperties_ShouldSetCorrectValues()
    {
        var id = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var exchangeRate = new ExchangeRate
        {
            Id = id,
            FromCurrency = "USD",
            ToCurrency = "EUR",
            Rate = 0.85m,
            Provider = "ECB",
            IsTransitive = true,
            IntermediateCurrency = "GBP",
            FetchedAt = now.AddHours(-1),
            ExpiresAt = now.AddHours(23),
            CreatedAt = now,
            UpdatedAt = now
        };

        Assert.Equal(id, exchangeRate.Id);
        Assert.Equal("USD", exchangeRate.FromCurrency);
        Assert.Equal("EUR", exchangeRate.ToCurrency);
        Assert.Equal(0.85m, exchangeRate.Rate);
        Assert.Equal("ECB", exchangeRate.Provider);
        Assert.True(exchangeRate.IsTransitive);
        Assert.Equal("GBP", exchangeRate.IntermediateCurrency);
    }

    [Fact]
    public void ExchangeRate_HasRequiredAttributes()
    {
        var context = CreateInMemoryDbContext();
        var model = context.Model;
        var entityType = model.FindEntityType(typeof(ExchangeRate));

        Assert.NotNull(entityType);
        Assert.Equal("exchange_rates", entityType.GetTableName());

        var fromCurrencyProperty = entityType.FindProperty(nameof(ExchangeRate.FromCurrency));
        Assert.NotNull(fromCurrencyProperty);
        Assert.False(fromCurrencyProperty.IsNullable);
        Assert.Equal(3, fromCurrencyProperty.GetMaxLength());

        var rateProperty = entityType.FindProperty(nameof(ExchangeRate.Rate));
        Assert.NotNull(rateProperty);
        Assert.False(rateProperty.IsNullable);
    }

    #endregion

    #region CountryCurrency Entity Tests

    [Fact]
    public void CountryCurrency_DefaultConstructor_ShouldInitializeDefaultValues()
    {
        var countryCurrency = new CountryCurrency();

        Assert.Equal(string.Empty, countryCurrency.CountryIso2);
        Assert.Equal(string.Empty, countryCurrency.CountryIso3);
        Assert.Equal(string.Empty, countryCurrency.CurrencyCode);
        Assert.True(countryCurrency.IsPrimary);
        Assert.Null(countryCurrency.Currency);
    }

    [Fact]
    public void CountryCurrency_SetProperties_ShouldSetCorrectValues()
    {
        var id = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var countryCurrency = new CountryCurrency
        {
            Id = id,
            CountryIso2 = "TH",
            CountryIso3 = "THA",
            CurrencyCode = "THB",
            IsPrimary = true,
            CreatedAt = now,
            Currency = new Currency { Code = "THB" }
        };

        Assert.Equal(id, countryCurrency.Id);
        Assert.Equal("TH", countryCurrency.CountryIso2);
        Assert.Equal("THA", countryCurrency.CountryIso3);
        Assert.Equal("THB", countryCurrency.CurrencyCode);
        Assert.True(countryCurrency.IsPrimary);
        Assert.NotNull(countryCurrency.Currency);
    }

    [Fact]
    public void CountryCurrency_HasRequiredAttributes()
    {
        var context = CreateInMemoryDbContext();
        var model = context.Model;
        var entityType = model.FindEntityType(typeof(CountryCurrency));

        Assert.NotNull(entityType);
        Assert.Equal("country_currencies", entityType.GetTableName());

        var countryIso2Property = entityType.FindProperty(nameof(CountryCurrency.CountryIso2));
        Assert.NotNull(countryIso2Property);
        Assert.False(countryIso2Property.IsNullable);
        Assert.Equal(2, countryIso2Property.GetMaxLength());

        var currencyCodeProperty = entityType.FindProperty(nameof(CountryCurrency.CurrencyCode));
        Assert.NotNull(currencyCodeProperty);
        Assert.False(currencyCodeProperty.IsNullable);
    }

    #endregion

    #region RateSnapshot Entity Tests

    [Fact]
    public void RateSnapshot_DefaultConstructor_ShouldInitializeDefaultValues()
    {
        var rateSnapshot = new RateSnapshot();

        Assert.Equal(Guid.Empty, rateSnapshot.BatchId);
        Assert.Equal(string.Empty, rateSnapshot.FromCurrency);
        Assert.Equal(string.Empty, rateSnapshot.ToCurrency);
        Assert.Equal(0m, rateSnapshot.Rate);
        Assert.Null(rateSnapshot.Source);
    }

    [Fact]
    public void RateSnapshot_SetProperties_ShouldSetCorrectValues()
    {
        var id = Guid.NewGuid();
        var batchId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var snapshotDate = new DateOnly(2024, 1, 15);
        var rateSnapshot = new RateSnapshot
        {
            Id = id,
            BatchId = batchId,
            FromCurrency = "USD",
            ToCurrency = "EUR",
            Rate = 0.92m,
            SnapshotDate = snapshotDate,
            Source = "ECB",
            CreatedAt = now
        };

        Assert.Equal(id, rateSnapshot.Id);
        Assert.Equal(batchId, rateSnapshot.BatchId);
        Assert.Equal("USD", rateSnapshot.FromCurrency);
        Assert.Equal("EUR", rateSnapshot.ToCurrency);
        Assert.Equal(0.92m, rateSnapshot.Rate);
        Assert.Equal(snapshotDate, rateSnapshot.SnapshotDate);
        Assert.Equal("ECB", rateSnapshot.Source);
    }

    [Fact]
    public void RateSnapshot_HasRequiredAttributes()
    {
        var context = CreateInMemoryDbContext();
        var model = context.Model;
        var entityType = model.FindEntityType(typeof(RateSnapshot));

        Assert.NotNull(entityType);
        Assert.Equal("rate_snapshots", entityType.GetTableName());

        var batchIdProperty = entityType.FindProperty(nameof(RateSnapshot.BatchId));
        Assert.NotNull(batchIdProperty);
        Assert.False(batchIdProperty.IsNullable);

        var rateProperty = entityType.FindProperty(nameof(RateSnapshot.Rate));
        Assert.NotNull(rateProperty);
        Assert.False(rateProperty.IsNullable);
    }

    #endregion

    #region StagedSnapshot Entity Tests

    [Fact]
    public void StagedSnapshot_DefaultConstructor_ShouldInitializeDefaultValues()
    {
        var stagedSnapshot = new StagedSnapshot();

        Assert.Equal(Guid.Empty, stagedSnapshot.BatchId);
        Assert.Equal(string.Empty, stagedSnapshot.FromCurrency);
        Assert.Equal(string.Empty, stagedSnapshot.ToCurrency);
        Assert.Equal(0m, stagedSnapshot.Rate);
        Assert.Equal("Pending", stagedSnapshot.Status);
        Assert.Null(stagedSnapshot.ValidationError);
    }

    [Fact]
    public void StagedSnapshot_SetProperties_ShouldSetCorrectValues()
    {
        var id = Guid.NewGuid();
        var batchId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var snapshotDate = new DateOnly(2024, 1, 15);
        var stagedSnapshot = new StagedSnapshot
        {
            Id = id,
            BatchId = batchId,
            FromCurrency = "USD",
            ToCurrency = "EUR",
            Rate = 0.92m,
            SnapshotDate = snapshotDate,
            Status = "Validated",
            ValidationError = null,
            CreatedAt = now
        };

        Assert.Equal(id, stagedSnapshot.Id);
        Assert.Equal(batchId, stagedSnapshot.BatchId);
        Assert.Equal("USD", stagedSnapshot.FromCurrency);
        Assert.Equal("EUR", stagedSnapshot.ToCurrency);
        Assert.Equal(0.92m, stagedSnapshot.Rate);
        Assert.Equal(snapshotDate, stagedSnapshot.SnapshotDate);
        Assert.Equal("Validated", stagedSnapshot.Status);
    }

    [Fact]
    public void StagedSnapshot_SetValidationError_ShouldSetCorrectly()
    {
        var stagedSnapshot = new StagedSnapshot
        {
            ValidationError = "Invalid rate: negative value"
        };

        Assert.Equal("Invalid rate: negative value", stagedSnapshot.ValidationError);
    }

    [Fact]
    public void StagedSnapshot_HasRequiredAttributes()
    {
        var context = CreateInMemoryDbContext();
        var model = context.Model;
        var entityType = model.FindEntityType(typeof(StagedSnapshot));

        Assert.NotNull(entityType);
        Assert.Equal("staged_snapshots", entityType.GetTableName());

        var statusProperty = entityType.FindProperty(nameof(StagedSnapshot.Status));
        Assert.NotNull(statusProperty);
        Assert.False(statusProperty.IsNullable);
        Assert.Equal(20, statusProperty.GetMaxLength());
    }

    #endregion

    #region AuditLog Entity Tests

    [Fact]
    public void AuditLog_DefaultConstructor_ShouldInitializeDefaultValues()
    {
        var auditLog = new AuditLog();

        Assert.Equal(string.Empty, auditLog.EntityType);
        Assert.Equal(string.Empty, auditLog.EntityId);
        Assert.Equal(string.Empty, auditLog.Operation);
        Assert.Null(auditLog.ChangedFields);
        Assert.Null(auditLog.UserId);
    }

    [Fact]
    public void AuditLog_SetProperties_ShouldSetCorrectValues()
    {
        var id = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var auditLog = new AuditLog
        {
            Id = id,
            EntityType = "Currency",
            EntityId = "123e4567-e89b-12d3-a456-426614174000",
            Operation = "Modified",
            ChangedFields = "[\"Name\", \"Symbol\"]",
            Timestamp = now,
            UserId = "admin@example.com"
        };

        Assert.Equal(id, auditLog.Id);
        Assert.Equal("Currency", auditLog.EntityType);
        Assert.Equal("123e4567-e89b-12d3-a456-426614174000", auditLog.EntityId);
        Assert.Equal("Modified", auditLog.Operation);
        Assert.Equal("[\"Name\", \"Symbol\"]", auditLog.ChangedFields);
        Assert.Equal(now, auditLog.Timestamp);
        Assert.Equal("admin@example.com", auditLog.UserId);
    }

    [Fact]
    public void AuditLog_HasRequiredAttributes()
    {
        var context = CreateInMemoryDbContext();
        var model = context.Model;
        var entityType = model.FindEntityType(typeof(AuditLog));

        Assert.NotNull(entityType);
        var tableName = entityType.GetTableName();
        Assert.NotNull(tableName);
        Assert.True(tableName is "audit_log" or "audit_logs", $"Expected audit_log or audit_logs, got {tableName}");

        var entityTypeProperty = entityType.FindProperty(nameof(AuditLog.EntityType));
        Assert.NotNull(entityTypeProperty);
        Assert.False(entityTypeProperty.IsNullable);

        var operationProperty = entityType.FindProperty(nameof(AuditLog.Operation));
        Assert.NotNull(operationProperty);
        Assert.False(operationProperty.IsNullable);
    }

    #endregion

    #region BatchStatus Entity Tests

    [Fact]
    public void BatchStatus_DefaultConstructor_ShouldInitializeDefaultValues()
    {
        var batchStatus = new BatchStatus();

        Assert.Equal(string.Empty, batchStatus.BatchId);
        Assert.Equal("Queued", batchStatus.Status);
        Assert.Null(batchStatus.ErrorMessage);
    }

    [Fact]
    public void BatchStatus_SetProperties_ShouldSetCorrectValues()
    {
        var id = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var batchId = "batch-123";
        var batchStatus = new BatchStatus
        {
            Id = id,
            BatchId = batchId,
            Status = "Completed",
            ErrorMessage = null,
            CreatedAt = now,
            UpdatedAt = now
        };

        Assert.Equal(id, batchStatus.Id);
        Assert.Equal(batchId, batchStatus.BatchId);
        Assert.Equal("Completed", batchStatus.Status);
    }

    [Fact]
    public void BatchStatus_SetErrorMessage_ShouldSetCorrectly()
    {
        var batchStatus = new BatchStatus
        {
            ErrorMessage = "Failed to connect to database"
        };

        Assert.Equal("Failed to connect to database", batchStatus.ErrorMessage);
    }

    [Fact]
    public void BatchStatus_HasRequiredAttributes()
    {
        var context = CreateInMemoryDbContext();
        var model = context.Model;
        var entityType = model.FindEntityType(typeof(BatchStatus));

        Assert.NotNull(entityType);
        Assert.Equal("batch_status", entityType.GetTableName());

        var statusProperty = entityType.FindProperty(nameof(BatchStatus.Status));
        Assert.NotNull(statusProperty);
        Assert.False(statusProperty.IsNullable);
        Assert.Equal(256, statusProperty.GetMaxLength());
    }

    #endregion

    #region Configuration Mapping Tests

    [Fact]
    public void CurrencyConfiguration_HasCorrectTableName()
    {
        using var context = CreateInMemoryDbContext();
        var model = context.Model;
        var entityType = model.FindEntityType(typeof(Currency));

        Assert.NotNull(entityType);
        Assert.Equal("currencies", entityType.GetTableName());
    }

    [Fact]
    public void CurrencyConfiguration_HasAlternateKeyOnCode()
    {
        using var context = CreateInMemoryDbContext();
        var model = context.Model;
        var entityType = model.FindEntityType(typeof(Currency));

        Assert.NotNull(entityType);
        var keys = entityType.GetKeys();
        Assert.NotEmpty(keys);
        var alternateKeys = keys.Where(k => k.Properties.Any(p => p.Name == "Code") && k.Properties.Count == 1).ToList();
        Assert.NotEmpty(alternateKeys);
    }

    [Fact]
    public void ExchangeRateConfiguration_HasCompositeIndex()
    {
        using var context = CreateInMemoryDbContext();
        var model = context.Model;
        var entityType = model.FindEntityType(typeof(ExchangeRate));

        Assert.NotNull(entityType);
        var indexes = entityType.GetIndexes();
        Assert.NotEmpty(indexes);
    }

    [Fact]
    public void CountryCurrencyConfiguration_HasForeignKey()
    {
        using var context = CreateInMemoryDbContext();
        var model = context.Model;
        var entityType = model.FindEntityType(typeof(CountryCurrency));

        Assert.NotNull(entityType);
        var foreignKeys = entityType.GetForeignKeys();
        Assert.NotEmpty(foreignKeys);
        var fk = foreignKeys.FirstOrDefault();
        Assert.NotNull(fk);
        Assert.Equal(DeleteBehavior.Restrict, fk.DeleteBehavior);
    }

    [Fact]
    public void RateSnapshotConfiguration_HasUniqueConstraint()
    {
        using var context = CreateInMemoryDbContext();
        var model = context.Model;
        var entityType = model.FindEntityType(typeof(RateSnapshot));

        Assert.NotNull(entityType);
        var indexes = entityType.GetIndexes();
        Assert.NotEmpty(indexes);
    }

    [Fact]
    public void StagedSnapshotConfiguration_HasDefaultStatus()
    {
        using var context = CreateInMemoryDbContext();
        var model = context.Model;
        var entityType = model.FindEntityType(typeof(StagedSnapshot));

        Assert.NotNull(entityType);
        var statusProperty = entityType.FindProperty(nameof(StagedSnapshot.Status));
        Assert.NotNull(statusProperty);
    }

    #endregion
}
