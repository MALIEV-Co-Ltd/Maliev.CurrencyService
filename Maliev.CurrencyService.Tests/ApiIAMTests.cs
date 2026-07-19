using Maliev.Aspire.ServiceDefaults.IAM;
using Maliev.CurrencyService.Api.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using RoleRegistration = Maliev.Aspire.ServiceDefaults.IAM.RoleRegistration;

namespace Maliev.CurrencyService.Tests;

public class ApiIAMTests
{
    private readonly Mock<IConfiguration> _configurationMock;
    private readonly Mock<ILogger<CurrencyIAMRegistrationService>> _loggerMock;

    public ApiIAMTests()
    {
        _configurationMock = new Mock<IConfiguration>();
        _loggerMock = new Mock<ILogger<CurrencyIAMRegistrationService>>();
    }

    private CurrencyIAMRegistrationService CreateService()
    {
        return new CurrencyIAMRegistrationService(_configurationMock.Object, _loggerMock.Object);
    }

    [Fact]
    public void CurrencyIAMRegistrationService_CanBeInstantiated()
    {
        var service = CreateService();

        Assert.NotNull(service);
    }

    [Fact]
    public void GetPermissions_ReturnsAllCurrencyPermissions()
    {
        var service = CreateService();
        var method = typeof(CurrencyIAMRegistrationService).GetMethod("GetPermissions", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        var permissions = method?.Invoke(service, null) as IEnumerable<PermissionRegistration>;

        Assert.NotNull(permissions);
        var permissionList = permissions!.ToList();

        Assert.Equal(CurrencyPermissions.AllWithDescriptions.Count, permissionList.Count);

        foreach (var (permissionId, description) in CurrencyPermissions.AllWithDescriptions)
        {
            var permission = permissionList.FirstOrDefault(p => p.PermissionId == permissionId);
            Assert.NotNull(permission);
            Assert.Equal(description, permission.Description);
        }
    }

    [Fact]
    public void GetPermissions_ContainsCurrenciesPermissions()
    {
        var service = CreateService();
        var method = typeof(CurrencyIAMRegistrationService).GetMethod("GetPermissions", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        var permissions = method?.Invoke(service, null) as IEnumerable<PermissionRegistration>;
        var permissionIds = permissions!.Select(p => p.PermissionId).ToList();

        Assert.Contains(CurrencyPermissions.CurrenciesRead, permissionIds);
        Assert.Contains(CurrencyPermissions.CurrenciesCreate, permissionIds);
        Assert.Contains(CurrencyPermissions.CurrenciesUpdate, permissionIds);
        Assert.Contains(CurrencyPermissions.CurrenciesDelete, permissionIds);
        Assert.Contains(CurrencyPermissions.CurrenciesActivate, permissionIds);
    }

    [Fact]
    public void GetPermissions_ContainsRatesPermissions()
    {
        var service = CreateService();
        var method = typeof(CurrencyIAMRegistrationService).GetMethod("GetPermissions", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        var permissions = method?.Invoke(service, null) as IEnumerable<PermissionRegistration>;
        var permissionIds = permissions!.Select(p => p.PermissionId).ToList();

        Assert.Contains(CurrencyPermissions.RatesRead, permissionIds);
        Assert.Contains(CurrencyPermissions.RatesUpdate, permissionIds);
        Assert.Contains(CurrencyPermissions.RatesBulkUpdate, permissionIds);
        Assert.Contains(CurrencyPermissions.RatesSetSource, permissionIds);
    }

    [Fact]
    public void GetPermissions_ContainsSnapshotsPermissions()
    {
        var service = CreateService();
        var method = typeof(CurrencyIAMRegistrationService).GetMethod("GetPermissions", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        var permissions = method?.Invoke(service, null) as IEnumerable<PermissionRegistration>;
        var permissionIds = permissions!.Select(p => p.PermissionId).ToList();

        Assert.Contains(CurrencyPermissions.SnapshotsRead, permissionIds);
        Assert.Contains(CurrencyPermissions.SnapshotsCreate, permissionIds);
        Assert.Contains(CurrencyPermissions.SnapshotsDelete, permissionIds);
        Assert.Contains(CurrencyPermissions.SnapshotsAudit, permissionIds);
    }

    [Fact]
    public void GetPermissions_ContainsSystemPermissions()
    {
        var service = CreateService();
        var method = typeof(CurrencyIAMRegistrationService).GetMethod("GetPermissions", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        var permissions = method?.Invoke(service, null) as IEnumerable<PermissionRegistration>;
        var permissionIds = permissions!.Select(p => p.PermissionId).ToList();

        Assert.Contains(CurrencyPermissions.SystemRefreshRates, permissionIds);
        Assert.Contains(CurrencyPermissions.SystemRebuildCache, permissionIds);
        Assert.Contains(CurrencyPermissions.SystemViewStats, permissionIds);
    }

    [Fact]
    public void GetPredefinedRoles_ReturnsAllPredefinedRoles()
    {
        var service = CreateService();
        var method = typeof(CurrencyIAMRegistrationService).GetMethod("GetPredefinedRoles", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        var roles = method?.Invoke(service, null) as IEnumerable<RoleRegistration>;

        Assert.NotNull(roles);
        var roleList = roles!.ToList();

        Assert.Equal(CurrencyPredefinedRoles.All.Count, roleList.Count);
    }

    [Fact]
    public void GetPredefinedRoles_AdminRoleHasAllPermissions()
    {
        var service = CreateService();
        var method = typeof(CurrencyIAMRegistrationService).GetMethod("GetPredefinedRoles", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        var roles = method?.Invoke(service, null) as IEnumerable<RoleRegistration>;
        var adminRole = roles!.FirstOrDefault(r => r.RoleId == CurrencyPredefinedRoles.Admin);

        Assert.NotNull(adminRole);
        Assert.False(adminRole!.IsCustom);
        Assert.Equal("Full administrative control over currencies and exchange rates", adminRole.Description);

        var allPermissionIds = CurrencyPermissions.All;
        foreach (var permissionId in allPermissionIds)
        {
            Assert.Contains(permissionId, adminRole.PermissionIds!);
        }
    }

    [Fact]
    public void GetPredefinedRoles_ViewerRoleHasReadOnlyPermissions()
    {
        var service = CreateService();
        var method = typeof(CurrencyIAMRegistrationService).GetMethod("GetPredefinedRoles", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        var roles = method?.Invoke(service, null) as IEnumerable<RoleRegistration>;
        var viewerRole = roles!.FirstOrDefault(r => r.RoleId == CurrencyPredefinedRoles.Viewer);

        Assert.NotNull(viewerRole);
        Assert.False(viewerRole!.IsCustom);
        Assert.Equal("Read-only access to currency data and exchange rates", viewerRole.Description);

        Assert.Contains(CurrencyPermissions.CurrenciesRead, viewerRole.PermissionIds!);
        Assert.Contains(CurrencyPermissions.RatesRead, viewerRole.PermissionIds!);
        Assert.Contains(CurrencyPermissions.SnapshotsRead, viewerRole.PermissionIds!);

        Assert.DoesNotContain(CurrencyPermissions.CurrenciesCreate, viewerRole.PermissionIds!);
        Assert.DoesNotContain(CurrencyPermissions.CurrenciesUpdate, viewerRole.PermissionIds!);
        Assert.DoesNotContain(CurrencyPermissions.CurrenciesDelete, viewerRole.PermissionIds!);
    }

    [Fact]
    public void GetPredefinedRoles_AllRolesAreNotCustom()
    {
        var service = CreateService();
        var method = typeof(CurrencyIAMRegistrationService).GetMethod("GetPredefinedRoles", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        var roles = method?.Invoke(service, null) as IEnumerable<RoleRegistration>;

        foreach (var role in roles!)
        {
            Assert.False(role.IsCustom);
        }
    }
}
