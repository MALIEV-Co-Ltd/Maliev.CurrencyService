namespace Maliev.CurrencyService.Api.Services;

public static class CurrencyPermissions
{
    public const string CurrenciesRead = "currency.currencies.read";
    public const string CurrenciesCreate = "currency.currencies.create";
    public const string CurrenciesUpdate = "currency.currencies.update";
    public const string CurrenciesDelete = "currency.currencies.delete";
    public const string CurrenciesActivate = "currency.currencies.activate";
    
    public const string RatesRead = "currency.rates.read";
    public const string RatesUpdate = "currency.rates.update";
    public const string RatesBulkUpdate = "currency.rates.bulk-update";
    public const string RatesSetSource = "currency.rates.set-source";
    
    public const string SnapshotsRead = "currency.snapshots.read";
    public const string SnapshotsCreate = "currency.snapshots.create";
    public const string SnapshotsDelete = "currency.snapshots.delete";
    public const string SnapshotsAudit = "currency.snapshots.audit";
    
    public const string SystemRefreshRates = "currency.system.refresh-rates";
    public const string SystemRebuildCache = "currency.system.rebuild-cache";
    public const string SystemViewStats = "currency.system.view-stats";

    public static string[] All => new[]
    {
        CurrenciesRead, CurrenciesCreate, CurrenciesUpdate, CurrenciesDelete, CurrenciesActivate,
        RatesRead, RatesUpdate, RatesBulkUpdate, RatesSetSource,
        SnapshotsRead, SnapshotsCreate, SnapshotsDelete, SnapshotsAudit,
        SystemRefreshRates, SystemRebuildCache, SystemViewStats
    };
}

public class PredefinedRole
{
    public string RoleId { get; init; } = default!;
    public string Description { get; init; } = default!;
    public string[] Permissions { get; init; } = default!;
}

public static class CurrencyPredefinedRoles
{
    public static readonly PredefinedRole Admin = new()
    {
        RoleId = "currency.admin",
        Description = "Currency Service Administrator",
        Permissions = CurrencyPermissions.All
    };

    public static readonly PredefinedRole Viewer = new()
    {
        RoleId = "currency.viewer",
        Description = "Currency Service Viewer",
        Permissions = new[] { 
            CurrencyPermissions.CurrenciesRead, 
            CurrencyPermissions.RatesRead,
            CurrencyPermissions.SnapshotsRead
        }
    };

    public static string[] AllRoleIds => new[] { Admin.RoleId, Viewer.RoleId };
    public static PredefinedRole[] All => new[] { Admin, Viewer };
}
