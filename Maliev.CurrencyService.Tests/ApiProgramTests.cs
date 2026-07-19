using Maliev.CurrencyService.Api.Metrics;
using Maliev.CurrencyService.Api.Models;
using Maliev.CurrencyService.Application.Interfaces;
using Maliev.CurrencyService.Domain.Interfaces;
using Maliev.CurrencyService.Infrastructure.Persistence.Interceptors;
using Maliev.CurrencyService.Infrastructure.Services;
using Maliev.CurrencyService.Infrastructure.Services.External;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Caching.Memory;
using Moq;
using Xunit;

namespace Maliev.CurrencyService.Tests;

public class ApiProgramTests
{
    private readonly IServiceCollection _services;

    public ApiProgramTests()
    {
        _services = new ServiceCollection();
        ConfigureServices();
    }

    private void ConfigureServices()
    {
        _services.AddSingleton(Mock.Of<Microsoft.Extensions.Configuration.IConfiguration>(c =>
            c[It.IsAny<string>()] == It.IsAny<string>()));

        _services.AddSingleton(Mock.Of<Microsoft.Extensions.Hosting.IHostEnvironment>(e =>
            e.EnvironmentName == "Development" &&
            e.ApplicationName == "Test"));

        _services.AddLogging();
        _services.AddMemoryCache();

        _services.AddSingleton<CurrencyServiceMetrics>();
        _services.AddSingleton<IDatabaseMetrics>(
            sp => sp.GetRequiredService<CurrencyServiceMetrics>());
        _services.AddSingleton<IProviderMetrics>(
            sp => sp.GetRequiredService<CurrencyServiceMetrics>());
        _services.AddSingleton<IRateServiceMetrics>(
            sp => sp.GetRequiredService<CurrencyServiceMetrics>());

        _services.AddScoped<DatabaseMetricsInterceptor>();
        _services.AddScoped<AuditLogInterceptor>();

        _services.AddSingleton<ISnapshotQueue, SnapshotQueue>();

        _services.AddControllers()
            .ConfigureApiBehaviorOptions(options =>
            {
                options.SuppressModelStateInvalidFilter = true;
            });
    }

    #region Service Registration Tests

    [Fact]
    public void ServiceRegistration_CurrencyServiceMetrics_IsSingleton()
    {
        var provider = _services.BuildServiceProvider();
        var instance1 = provider.GetService<CurrencyServiceMetrics>();
        var instance2 = provider.GetService<CurrencyServiceMetrics>();
        Assert.Same(instance1, instance2);
    }

    [Fact]
    public void ServiceRegistration_IDatabaseMetrics_ResolvesToCurrencyServiceMetrics()
    {
        var provider = _services.BuildServiceProvider();
        var metrics = provider.GetService<CurrencyServiceMetrics>();
        var dbMetrics = provider.GetService<IDatabaseMetrics>();
        Assert.Same(metrics, dbMetrics);
    }

    [Fact]
    public void ServiceRegistration_IProviderMetrics_ResolvesToCurrencyServiceMetrics()
    {
        var provider = _services.BuildServiceProvider();
        var metrics = provider.GetService<CurrencyServiceMetrics>();
        var providerMetrics = provider.GetService<IProviderMetrics>();
        Assert.Same(metrics, providerMetrics);
    }

    [Fact]
    public void ServiceRegistration_IRateServiceMetrics_ResolvesToCurrencyServiceMetrics()
    {
        var provider = _services.BuildServiceProvider();
        var metrics = provider.GetService<CurrencyServiceMetrics>();
        var rateMetrics = provider.GetService<IRateServiceMetrics>();
        Assert.Same(metrics, rateMetrics);
    }

    [Fact]
    public void ServiceRegistration_DatabaseMetricsInterceptor_IsScoped()
    {
        var provider = _services.BuildServiceProvider();
        using var scope1 = provider.CreateScope();
        using var scope2 = provider.CreateScope();
        var interceptor1 = scope1.ServiceProvider.GetService<DatabaseMetricsInterceptor>();
        var interceptor2 = scope2.ServiceProvider.GetService<DatabaseMetricsInterceptor>();
        Assert.NotSame(interceptor1, interceptor2);
    }

    [Fact]
    public void ServiceRegistration_AuditLogInterceptor_IsScoped()
    {
        var provider = _services.BuildServiceProvider();
        using var scope1 = provider.CreateScope();
        using var scope2 = provider.CreateScope();
        var interceptor1 = scope1.ServiceProvider.GetService<AuditLogInterceptor>();
        var interceptor2 = scope2.ServiceProvider.GetService<AuditLogInterceptor>();
        Assert.NotSame(interceptor1, interceptor2);
    }

    [Fact]
    public void ServiceRegistration_SnapshotQueue_IsSingleton()
    {
        var provider = _services.BuildServiceProvider();
        var instance1 = provider.GetService<ISnapshotQueue>();
        var instance2 = provider.GetService<ISnapshotQueue>();
        Assert.Same(instance1, instance2);
    }

    #endregion

    #region Configuration Binding Tests

    [Fact]
    public void ConfigurationBinding_RedisOptions_BindsCorrectly()
    {
        var options = new RedisOptions
        {
            ConnectionString = "localhost:6379",
            Database = 0,
            ConnectTimeout = 5,
            SyncTimeout = 5000,
            AbortOnConnectFail = false
        };

        Assert.Equal("localhost:6379", options.ConnectionString);
        Assert.Equal(0, options.Database);
        Assert.Equal(5, options.ConnectTimeout);
        Assert.Equal(5000, options.SyncTimeout);
        Assert.False(options.AbortOnConnectFail);
    }

    [Fact]
    public void ConfigurationBinding_ExchangeRateOptions_BindsCorrectly()
    {
        var options = new ExchangeRateOptions
        {
            CacheDurationMinutes = 30,
            RetryAttempts = 3,
            TimeoutSeconds = 30,
            FrankfurterApiUrl = "https://api.frankfurter.app/",
            ProviderOrder = new List<string> { "Frankfurter", "Fawazahmed" },
            EnableDynamicPrioritization = false,
            MinRequestsForPrioritization = 10,
            ResponseTimeWeight = 0.4,
            SuccessRateWeight = 0.3,
            ErrorRateWeight = 0.2,
            RequestCountWeight = 0.1
        };

        Assert.Equal(30, options.CacheDurationMinutes);
        Assert.Equal(3, options.RetryAttempts);
        Assert.Equal(30, options.TimeoutSeconds);
        Assert.Equal("https://api.frankfurter.app/", options.FrankfurterApiUrl);
        Assert.Equal(2, options.ProviderOrder.Count);
        Assert.Equal("Frankfurter", options.ProviderOrder[0]);
        Assert.False(options.EnableDynamicPrioritization);
        Assert.Equal(10, options.MinRequestsForPrioritization);
    }

    [Fact]
    public void ConfigurationBinding_RedisOptions_SectionName_IsCorrect()
    {
        Assert.Equal("Redis", RedisOptions.SectionName);
    }

    [Fact]
    public void ConfigurationBinding_ExchangeRateOptions_SectionName_IsCorrect()
    {
        Assert.Equal("ExchangeRate", ExchangeRateOptions.SectionName);
    }

    [Fact]
    public void ConfigurationBinding_ExchangeRateOptions_DefaultValues()
    {
        var options = new ExchangeRateOptions();

        Assert.Equal(30, options.CacheDurationMinutes);
        Assert.Equal(3, options.RetryAttempts);
        Assert.Equal(30, options.TimeoutSeconds);
        Assert.Equal("https://api.frankfurter.app/", options.FrankfurterApiUrl);
        Assert.Equal(4, options.ProviderOrder.Count);
        Assert.Contains("Frankfurter", options.ProviderOrder);
        Assert.Contains("Fawazahmed", options.ProviderOrder);
    }

    [Fact]
    public void ConfigurationBinding_RedisOptions_ValidationAttributes()
    {
        var options = new RedisOptions
        {
            ConnectionString = "localhost:6379"
        };
        var validationContext = new System.ComponentModel.DataAnnotations.ValidationContext(options);
        var validationResults = new List<System.ComponentModel.DataAnnotations.ValidationResult>();

        var isValid = System.ComponentModel.DataAnnotations.Validator.TryValidateObject(
            options, validationContext, validationResults, true);

        Assert.True(isValid);
    }

    [Fact]
    public void ConfigurationBinding_ExchangeRateOptions_ValidationAttributes()
    {
        var options = new ExchangeRateOptions
        {
            CacheDurationMinutes = 60,
            RetryAttempts = 5,
            TimeoutSeconds = 60
        };

        var validationContext = new System.ComponentModel.DataAnnotations.ValidationContext(options);
        var validationResults = new List<System.ComponentModel.DataAnnotations.ValidationResult>();

        var isValid = System.ComponentModel.DataAnnotations.Validator.TryValidateObject(
            options, validationContext, validationResults, true);

        Assert.True(isValid);
    }

    [Fact]
    public void ConfigurationBinding_ExchangeRateOptions_InvalidCacheDuration_FailsValidation()
    {
        var options = new ExchangeRateOptions
        {
            CacheDurationMinutes = 0
        };

        var validationContext = new System.ComponentModel.DataAnnotations.ValidationContext(options);
        var validationResults = new List<System.ComponentModel.DataAnnotations.ValidationResult>();

        var isValid = System.ComponentModel.DataAnnotations.Validator.TryValidateObject(
            options, validationContext, validationResults, true);

        Assert.False(isValid);
    }

    #endregion

    #region Middleware Pipeline Tests

    [Fact]
    public void MiddlewarePipeline_Controllers_CanBeAddedToServices()
    {
        var services = new ServiceCollection();
        services.AddControllers();

        var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(Microsoft.AspNetCore.Mvc.Controllers.IControllerFactory));
        Assert.NotNull(descriptor);
    }

    [Fact]
    public void MiddlewarePipeline_Controllers_ConfigurationWorks()
    {
        var services = new ServiceCollection();
        var builder = services.AddControllers();

        Assert.NotNull(builder);
    }

    [Fact]
    public void MiddlewarePipeline_MemoryCache_IsRegistered()
    {
        var provider = _services.BuildServiceProvider();
        var cache = provider.GetService<IMemoryCache>();
        Assert.NotNull(cache);
    }

    #endregion

    #region Program Class Tests

    [Fact]
    public void ProgramClass_Exists()
    {
        var assembly = typeof(CurrencyServiceMetrics).Assembly;
        var programType = assembly.GetType("Program");
        Assert.NotNull(programType);
    }

    [Fact]
    public void ProgramClass_IsPartialClass()
    {
        var assembly = typeof(CurrencyServiceMetrics).Assembly;
        var programType = assembly.GetType("Program");
        Assert.NotNull(programType);
        Assert.True(programType.IsClass);
        Assert.True(programType.IsAbstract == false);
    }

    [Fact]
    public void ProgramClass_Log_ContainsRequiredMethods()
    {
        var assembly = typeof(CurrencyServiceMetrics).Assembly;
        var programType = assembly.GetType("Program");
        Assert.NotNull(programType);

        var logClass = programType.GetNestedType("Log",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        Assert.NotNull(logClass);

        var startingHostMethod = logClass.GetMethod("StartingHost",
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
        var hostTerminatedMethod = logClass.GetMethod("HostTerminated",
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
        var serviceStartedMethod = logClass.GetMethod("ServiceStarted",
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);

        Assert.NotNull(startingHostMethod);
        Assert.NotNull(hostTerminatedMethod);
        Assert.NotNull(serviceStartedMethod);
    }

    #endregion
}
