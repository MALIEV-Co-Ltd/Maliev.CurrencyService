using Maliev.Aspire.ServiceDefaults.IAM;
using RoleRegistration = Maliev.Aspire.ServiceDefaults.IAM.RoleRegistration;

namespace Maliev.CurrencyService.Api.Services;

/// <summary>Service that handles registration of permissions and roles with the central IAM service on startup.</summary>
public class CurrencyIAMRegistrationService : IAMRegistrationService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<CurrencyIAMRegistrationService> _logger;
    private readonly IHostApplicationLifetime _hostApplicationLifetime;

    /// <summary>Initializes a new instance of the <see cref="CurrencyIAMRegistrationService"/> class.</summary>
    public CurrencyIAMRegistrationService(
        IHttpClientFactory httpClientFactory,
        ILogger<CurrencyIAMRegistrationService> logger,
        IConfiguration configuration,
        IHostApplicationLifetime hostApplicationLifetime)
        : base(httpClientFactory, logger, "currency")
    {
        _configuration = configuration;
        _logger = logger;
        _hostApplicationLifetime = hostApplicationLifetime;
    }

    /// <summary>Gets the list of permissions to register with IAM.</summary>
    protected override IEnumerable<PermissionRegistration> GetPermissions()
    {
        return CurrencyPermissions.All.Select(p => new PermissionRegistration
        {
            PermissionId = p,
            Description = $"Permission: {p}"
        });
    }

    /// <summary>Gets the list of predefined roles to register with IAM.</summary>
    protected override IEnumerable<RoleRegistration> GetPredefinedRoles()
    {
        return CurrencyPredefinedRoles.All.Select(r => new RoleRegistration
        {
            RoleId = r.RoleId,
            Description = r.Description,
            PermissionIds = r.Permissions.ToList()
        });
    }

    /// <summary>Registers permissions and roles with IAM.</summary>
    public async Task RegisterWithCheckAsync(CancellationToken cancellationToken)
    {
        try
        {
            await base.RegisterAsync(cancellationToken);
            _logger.LogInformation("Successfully registered {Count} permissions and {RoleCount} roles with IAM",
                CurrencyPermissions.All.Length, CurrencyPredefinedRoles.All.Length);
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "FAILED to register with IAM service. Fail Fast behavior triggered.");

            // Fail Fast: Terminate the application
            _hostApplicationLifetime.StopApplication();

            // Throwing to ensure the startup sequence is interrupted
            throw new ApplicationException("Mandatory IAM registration failed. Service cannot start.", ex);
        }
    }
}
