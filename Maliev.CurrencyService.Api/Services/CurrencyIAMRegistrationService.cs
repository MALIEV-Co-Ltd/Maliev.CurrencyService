using Maliev.Aspire.ServiceDefaults.IAM;
using RoleRegistration = Maliev.Aspire.ServiceDefaults.IAM.RoleRegistration;

namespace Maliev.CurrencyService.Api.Services;

/// <summary>Service that handles registration of permissions and roles with the central IAM service on startup.</summary>
public class CurrencyIAMRegistrationService : IAMRegistrationService
{
    /// <summary>Initializes a new instance of the <see cref="CurrencyIAMRegistrationService"/> class.</summary>
    public CurrencyIAMRegistrationService(
        IConfiguration configuration,
        ILogger<CurrencyIAMRegistrationService> logger)
        : base(configuration, logger, "currency")
    {
    }

    /// <summary>Gets the list of permissions to register with IAM.</summary>
    protected override IEnumerable<PermissionRegistration> GetPermissions()
    {
        return CurrencyPermissions.AllWithDescriptions.Select(p => new PermissionRegistration
        {
            PermissionId = p.Key,
            Description = p.Value
        });
    }

    /// <summary>Gets the list of predefined roles to register with IAM.</summary>
    protected override IEnumerable<RoleRegistration> GetPredefinedRoles()
    {
        return CurrencyPredefinedRoles.All.Select(r => new RoleRegistration
        {
            RoleId = r.RoleId,
            Description = r.Description,
            PermissionIds = r.Permissions.ToList(),
            IsCustom = false
        });
    }
}
