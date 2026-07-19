using System.Net;
using System.Net.Http.Headers;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Asp.Versioning;
using Maliev.Aspire.ServiceDefaults.Authorization;
using Maliev.Aspire.ServiceDefaults.IAM;
using Maliev.CurrencyService.Api.Controllers;
using Maliev.CurrencyService.Api.Services;
using Maliev.CurrencyService.Infrastructure.Services.External;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Options;

namespace Maliev.CurrencyService.Tests.Unit;

/// <summary>
/// Tests CurrencyService's outbound workload authentication boundary.
/// </summary>
public sealed class ServiceAuthenticationWiringTests
{
    private const string ExpectedToken = "centrally-issued-currency-token";

    /// <summary>
    /// CurrencyService startup should opt into AuthService exchange and the central IAM client only.
    /// </summary>
    [Fact]
    public void Program_RegistersCurrencyExchangeWithoutLegacySigner()
    {
        var source = ReadRepositoryFile("Maliev.CurrencyService.Api", "Program.cs");

        Assert.Contains("builder.AddAuthServiceTokenExchange(\"CurrencyService\");", source, StringComparison.Ordinal);
        Assert.Contains("builder.AddAuthServiceIAMClient();", source, StringComparison.Ordinal);
        Assert.DoesNotContain("AddIAMServiceClient", source, StringComparison.Ordinal);
    }

    /// <summary>
    /// The process identity should be exact and no local-signing services should resolve.
    /// </summary>
    [Fact]
    public void AuthServiceIamClient_RegistersExactIdentityWithoutLegacySigningServices()
    {
        var builder = CreateConfiguredBuilder();

        builder.AddAuthServiceTokenExchange("CurrencyService");
        builder.AddAuthServiceIAMClient();

        using var provider = builder.Services.BuildServiceProvider();
        var identity = provider.GetRequiredService<ServiceProcessIdentity>();

        Assert.Equal("CurrencyService", identity.ServiceName);
        Assert.Single(provider.GetServices<IIamServiceClient>());
        Assert.Null(provider.GetService<IServiceAccountTokenProvider>());
        Assert.Null(provider.GetService<ServiceAccountAuthenticationHandler>());
    }

    /// <summary>
    /// IAM permission checks should use the AuthService bearer on the exact POST route.
    /// </summary>
    [Fact]
    public async Task IamPermissionCheck_UsesAuthServiceExchangedBearerTokenOnExactPostRoute()
    {
        var builder = CreateConfiguredBuilder();
        var filter = new TrackingPrimaryHandlerFilter();
        builder.Services.AddSingleton<IHttpMessageHandlerBuilderFilter>(filter);

        builder.AddAuthServiceTokenExchange("CurrencyService");
        builder.Services.AddSingleton<IAuthServiceTokenProvider>(new StubTokenProvider());
        builder.AddAuthServiceIAMClient();

        await using var provider = builder.Services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var iamClient = scope.ServiceProvider.GetRequiredService<IIamServiceClient>();

        var allowed = await iamClient.CheckPermissionAsync(
            $"currency-test-{Guid.NewGuid():N}",
            CurrencyPermissions.CurrenciesRead,
            cancellationToken: CancellationToken.None);

        var capture = filter.GetCapture("IAMService");
        Assert.True(allowed);
        Assert.Equal(new AuthenticationHeaderValue("Bearer", ExpectedToken), capture.Authorization);
        Assert.Equal(HttpMethod.Post, capture.Method);
        Assert.Equal(new Uri("https://iam.test/iam/v1/auth/check-permission"), capture.RequestUri);
    }

    /// <summary>
    /// Missing or malformed workload credentials should fail options validation during host startup.
    /// </summary>
    [Theory]
    [InlineData(null, null)]
    [InlineData("service-currency-service", "short")]
    public async Task AuthServiceExchange_InvalidCredentials_FailsClosedAtHostStartup(
        string? clientId,
        string? clientSecret)
    {
        var builder = CreateConfiguredBuilder(clientId, clientSecret);
        builder.AddAuthServiceTokenExchange("CurrencyService");

        using var host = builder.Build();

        await Assert.ThrowsAsync<OptionsValidationException>(() => host.StartAsync());
    }

    /// <summary>
    /// CI should consume the exact published ServiceDefaults version containing central exchange support.
    /// </summary>
    [Fact]
    public void ServiceDefaultsDependency_PinsPublishedCentralExchangeVersion()
    {
        var source = ReadRepositoryFile("Directory.Build.props");

        Assert.Contains(
            "<ServiceDefaultsVersion Condition=\"'$(ServiceDefaultsVersion)' == ''\">1.0.89-alpha</ServiceDefaultsVersion>",
            source,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "<ServiceDefaultsVersion Condition=\"'$(ServiceDefaultsVersion)' == ''\">1.0.*",
            source,
            StringComparison.Ordinal);

        foreach (var project in new[]
                 {
                     "Maliev.CurrencyService.Api/Maliev.CurrencyService.Api.csproj",
                     "Maliev.CurrencyService.Application/Maliev.CurrencyService.Application.csproj",
                     "Maliev.CurrencyService.Infrastructure/Maliev.CurrencyService.Infrastructure.csproj",
                     "Maliev.CurrencyService.Tests/Maliev.CurrencyService.Tests.csproj"
                 })
        {
            var projectSource = ReadRepositoryFile(project.Split('/'));
            Assert.Contains(
                "<PackageReference Include=\"Maliev.Aspire.ServiceDefaults\" Version=\"$(ServiceDefaultsVersion)\" />",
                projectSource,
                StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// The Docker restore layer must include the shared version property before restoring package-mode projects.
    /// </summary>
    [Fact]
    public void Dockerfile_CopiesSharedVersionPropertiesBeforePackageRestore()
    {
        var source = ReadRepositoryFile("Maliev.CurrencyService.Api", "Dockerfile");
        var propertiesCopy = source.IndexOf(
            "COPY [\"Directory.Build.props\", \".\"]",
            StringComparison.Ordinal);
        var restore = source.IndexOf(
            "dotnet restore \"./Maliev.CurrencyService.Api/Maliev.CurrencyService.Api.csproj\"",
            StringComparison.Ordinal);

        Assert.True(propertiesCopy >= 0, "Dockerfile must copy Directory.Build.props into the restore layer.");
        Assert.True(restore > propertiesCopy, "Directory.Build.props must be available before dotnet restore.");
    }

    /// <summary>
    /// Currency routes and permission policies are part of the service contract and must remain unchanged.
    /// </summary>
    [Fact]
    public void CurrencyEndpoints_RetainVersionedRoutesAndPermissionPolicies()
    {
        AssertControllerRoute<CurrenciesController>("currency/v{version:apiVersion}/currencies");
        AssertEndpoint<CurrenciesController>(nameof(CurrenciesController.ListCurrencies), null, "GET", CurrencyPermissions.CurrenciesRead);
        AssertEndpoint<CurrenciesController>(nameof(CurrenciesController.GetCurrencyById), "{id:guid}", "GET", CurrencyPermissions.CurrenciesRead);
        AssertEndpoint<CurrenciesController>(nameof(CurrenciesController.GetCurrencyByCountryPath), "~/currency/v{version:apiVersion}/countries/{countryCode}/currency", "GET", CurrencyPermissions.CurrenciesRead);
        AssertEndpoint<CurrenciesController>(nameof(CurrenciesController.GetCurrencyByCountry), "by-country", "GET", CurrencyPermissions.CurrenciesRead);
        AssertEndpoint<CurrenciesController>(nameof(CurrenciesController.GetByCode), "{code}", "GET", CurrencyPermissions.CurrenciesRead);
        AssertEndpoint<CurrenciesController>(nameof(CurrenciesController.GetById), "~/currency/v{version:apiVersion}/admin/currencies/{id:guid}", "GET", CurrencyPermissions.CurrenciesRead);
        AssertEndpoint<CurrenciesController>(nameof(CurrenciesController.CreateAdmin), "~/currency/v{version:apiVersion}/admin/currencies", "POST", CurrencyPermissions.CurrenciesCreate);
        AssertEndpoint<CurrenciesController>(nameof(CurrenciesController.Update), "{code}", "PUT", CurrencyPermissions.CurrenciesUpdate);
        AssertEndpoint<CurrenciesController>(nameof(CurrenciesController.UpdateById), "~/currency/v{version:apiVersion}/admin/currencies/{id:guid}", "PUT", CurrencyPermissions.CurrenciesUpdate);
        AssertEndpoint<CurrenciesController>(nameof(CurrenciesController.DeleteById), "~/currency/v{version:apiVersion}/admin/currencies/{id:guid}", "DELETE", CurrencyPermissions.CurrenciesDelete);
        AssertEndpoint<CurrenciesController>(nameof(CurrenciesController.Delete), "{code}", "DELETE", CurrencyPermissions.CurrenciesDelete);
        AssertEndpoint<CurrenciesController>(nameof(CurrenciesController.Activate), "~/currency/v{version:apiVersion}/admin/currencies/{id:guid}/activate", "POST", CurrencyPermissions.CurrenciesActivate);
        AssertEndpoint<CurrenciesController>(nameof(CurrenciesController.Deactivate), "~/currency/v{version:apiVersion}/admin/currencies/{id:guid}/deactivate", "POST", CurrencyPermissions.CurrenciesActivate);

        AssertControllerRoute<RatesController>("currency/v{version:apiVersion}/rates");
        AssertEndpoint<RatesController>(nameof(RatesController.ConvertCurrency), "convert", "POST", CurrencyPermissions.RatesRead);
        AssertEndpoint<RatesController>(nameof(RatesController.GetExchangeRate), null, "GET", CurrencyPermissions.RatesRead);
        AssertEndpoint<RatesController>(nameof(RatesController.UpdateRate), null, "PUT", CurrencyPermissions.RatesUpdate);
        AssertEndpoint<RatesController>(nameof(RatesController.BulkUpdateRates), "bulk-update", "POST", CurrencyPermissions.RatesBulkUpdate);
        AssertEndpoint<RatesController>(nameof(RatesController.SetRateSource), "set-source", "POST", CurrencyPermissions.RatesSetSource);
        AssertEndpoint<RatesController>(nameof(RatesController.RefreshRatesFromProvider), "refresh", "POST", CurrencyPermissions.SystemRefreshRates);

        AssertControllerRoute<SnapshotsController>("currency/v{version:apiVersion}/admin/snapshots");
        AssertEndpoint<SnapshotsController>(nameof(SnapshotsController.ImportBatch), "ingest", "POST", CurrencyPermissions.SnapshotsCreate);
        AssertEndpoint<SnapshotsController>(nameof(SnapshotsController.PromoteBatch), "{batchId}/promote", "POST", CurrencyPermissions.SnapshotsCreate);
        AssertEndpoint<SnapshotsController>(nameof(SnapshotsController.CleanupOldSnapshots), "cleanup", "POST", CurrencyPermissions.SnapshotsDelete);
        AssertEndpoint<SnapshotsController>(nameof(SnapshotsController.GetBatchStatus), "{batchId}/status", "GET", CurrencyPermissions.SnapshotsRead);
        AssertEndpoint<SnapshotsController>(nameof(SnapshotsController.GetBatchAudit), "{batchId}/audit", "GET", CurrencyPermissions.SnapshotsAudit);

        AssertControllerRoute<SystemController>("currency/v{version:apiVersion}/system");
        AssertEndpoint<SystemController>(nameof(SystemController.RebuildCache), "rebuild-cache", "POST", CurrencyPermissions.SystemRebuildCache);
        AssertEndpoint<SystemController>(nameof(SystemController.GetStats), "stats", "GET", CurrencyPermissions.SystemViewStats);
    }

    /// <summary>
    /// AuthService exchange must not add its handler or bearer to either public exchange-rate provider.
    /// </summary>
    [Fact]
    public async Task AuthServiceExchange_DoesNotContaminateExchangeRateProviderClients()
    {
        var builder = CreateConfiguredBuilder();
        var filter = new TrackingPrimaryHandlerFilter();
        var tokenProvider = new CountingTokenProvider();
        builder.Services.AddSingleton<IHttpMessageHandlerBuilderFilter>(filter);
        builder.Services.AddHttpClient<FawazahmedProvider>().AddStandardResilienceHandler();
        builder.Services.AddHttpClient<FrankfurterProvider>().AddStandardResilienceHandler();

        builder.AddAuthServiceTokenExchange("CurrencyService");
        builder.Services.AddSingleton<IAuthServiceTokenProvider>(tokenProvider);
        builder.AddAuthServiceIAMClient();

        await using var provider = builder.Services.BuildServiceProvider();
        var factory = provider.GetRequiredService<IHttpClientFactory>();
        var providerNames = new[]
        {
            typeof(FawazahmedProvider).FullName!,
            typeof(FrankfurterProvider).FullName!
        };

        foreach (var providerName in providerNames)
        {
            var client = factory.CreateClient(providerName);
            using var response = await client.GetAsync("https://provider.test/probe", CancellationToken.None);
            var capture = filter.GetCapture(providerName);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.False(filter.HasAuthServiceHandler(providerName));
            Assert.Null(capture.Authorization);
            Assert.Equal(HttpMethod.Get, capture.Method);
            Assert.Equal(new Uri("https://provider.test/probe"), capture.RequestUri);
        }

        Assert.Equal(0, tokenProvider.CallCount);
    }

    private static HostApplicationBuilder CreateConfiguredBuilder(
        string? clientId = "service-currency-service",
        string? clientSecret = "currency-test-secret-with-at-least-32-bytes")
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            EnvironmentName = "Testing"
        });

        using var rsa = RSA.Create(2048);
        builder.Configuration["ServiceAuthentication:ClientId"] = clientId;
        builder.Configuration["ServiceAuthentication:ClientSecret"] = clientSecret;
        builder.Configuration["Services:AuthService:BaseUrl"] = "https://auth.test";
        builder.Configuration["Services:IAMService:BaseUrl"] = "https://iam.test";
        builder.Configuration["Jwt:PublicKey"] = Convert.ToBase64String(
            Encoding.UTF8.GetBytes(rsa.ExportSubjectPublicKeyInfoPem()));
        builder.Configuration["Jwt:Issuer"] = "https://api.maliev.com";
        builder.Configuration["Jwt:Audience"] = "https://api.maliev.com";

        return builder;
    }

    private static void AssertControllerRoute<TController>(string expectedTemplate)
    {
        var controller = typeof(TController);
        Assert.NotNull(controller.GetCustomAttribute<ApiVersionAttribute>());
        Assert.Equal(expectedTemplate, controller.GetCustomAttribute<RouteAttribute>()?.Template);
    }

    private static void AssertEndpoint<TController>(
        string methodName,
        string? expectedTemplate,
        string expectedVerb,
        string expectedPermission)
    {
        var method = typeof(TController).GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance);
        Assert.NotNull(method);

        var route = method.GetCustomAttributes<HttpMethodAttribute>().Single();
        Assert.Equal(expectedTemplate, route.Template);
        Assert.Contains(expectedVerb, route.HttpMethods);
        Assert.Equal(expectedPermission, method.GetCustomAttribute<RequirePermissionAttribute>()?.Permission);
    }

    private static string ReadRepositoryFile(params string[] segments)
    {
        var path = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            Path.Combine(segments)));

        Assert.True(File.Exists(path), $"Could not find source file: {path}");
        return File.ReadAllText(path);
    }

    private sealed class StubTokenProvider : IAuthServiceTokenProvider
    {
        public Task<string> GetTokenAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(ExpectedToken);
    }

    private sealed class CountingTokenProvider : IAuthServiceTokenProvider
    {
        public int CallCount { get; private set; }

        public Task<string> GetTokenAsync(CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult(ExpectedToken);
        }
    }

    private sealed class AuthorizationCaptureHandler : HttpMessageHandler
    {
        public AuthenticationHeaderValue? Authorization { get; private set; }

        public HttpMethod? Method { get; private set; }

        public Uri? RequestUri { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Authorization = request.Headers.Authorization;
            Method = request.Method;
            RequestUri = request.RequestUri;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"allowed\":true}", Encoding.UTF8, "application/json")
            });
        }
    }

    private sealed class TrackingPrimaryHandlerFilter : IHttpMessageHandlerBuilderFilter
    {
        private readonly Dictionary<string, AuthorizationCaptureHandler> _captures = new(StringComparer.Ordinal);
        private readonly Dictionary<string, bool> _authHandlers = new(StringComparer.Ordinal);

        public AuthorizationCaptureHandler GetCapture(string clientName) => _captures[clientName];

        public bool HasAuthServiceHandler(string clientName) => _authHandlers[clientName];

        public Action<HttpMessageHandlerBuilder> Configure(Action<HttpMessageHandlerBuilder> next) => builder =>
        {
            next(builder);
            var clientName = builder.Name
                ?? throw new InvalidOperationException("Every HttpClientFactory handler must have a client name.");
            _authHandlers[clientName] = builder.AdditionalHandlers.Any(
                handler => handler is AuthServiceTokenExchangeHandler);
            for (var index = builder.AdditionalHandlers.Count - 1; index >= 0; index--)
            {
                if (builder.AdditionalHandlers[index].GetType().FullName?.Contains(
                        "ServiceDiscovery",
                        StringComparison.Ordinal) == true ||
                    builder.AdditionalHandlers[index].GetType().FullName?.Contains(
                        "ResolvingHttpDelegatingHandler",
                        StringComparison.Ordinal) == true)
                {
                    builder.AdditionalHandlers.RemoveAt(index);
                }
            }

            var capture = new AuthorizationCaptureHandler();
            _captures[clientName] = capture;
            builder.PrimaryHandler = capture;
        };
    }
}
