namespace Maliev.CurrencyService.Api.Services;

/// <summary>
/// Defines permission constants for the Currency Service.
/// </summary>
public static class CurrencyPermissions
{
    /// <summary>Permission to read currencies.</summary>
    public const string CurrenciesRead = "currency.currencies.read";
    /// <summary>Permission to create currencies.</summary>
    public const string CurrenciesCreate = "currency.currencies.create";
    /// <summary>Permission to update currencies.</summary>
    public const string CurrenciesUpdate = "currency.currencies.update";
    /// <summary>Permission to delete currencies.</summary>
    public const string CurrenciesDelete = "currency.currencies.delete";
    /// <summary>Permission to activate/deactivate currencies.</summary>
    public const string CurrenciesActivate = "currency.currencies.activate";

    /// <summary>Permission to read exchange rates.</summary>
    public const string RatesRead = "currency.rates.read";
    /// <summary>Permission to update exchange rates.</summary>
    public const string RatesUpdate = "currency.rates.update";
    /// <summary>Permission to bulk update exchange rates.</summary>
    public const string RatesBulkUpdate = "currency.rates.bulk-update";
    /// <summary>Permission to set exchange rate data source.</summary>
    public const string RatesSetSource = "currency.rates.set-source";

    /// <summary>Permission to read snapshots.</summary>
    public const string SnapshotsRead = "currency.snapshots.read";
    /// <summary>Permission to create snapshots.</summary>
    public const string SnapshotsCreate = "currency.snapshots.create";
    /// <summary>Permission to delete snapshots.</summary>
    public const string SnapshotsDelete = "currency.snapshots.delete";
    /// <summary>Permission to audit snapshots.</summary>
    public const string SnapshotsAudit = "currency.snapshots.audit";

    /// <summary>Permission to trigger system rate refresh.</summary>
    public const string SystemRefreshRates = "currency.system.refresh-rates";
    /// <summary>Permission to trigger system cache rebuild.</summary>
    public const string SystemRebuildCache = "currency.system.rebuild-cache";
    /// <summary>Permission to view system statistics.</summary>
    public const string SystemViewStats = "currency.system.view-stats";

    /// <summary>
    /// Gets all defined permissions.
    /// </summary>
    public static string[] All => new[]
    {
        CurrenciesRead, CurrenciesCreate, CurrenciesUpdate, CurrenciesDelete, CurrenciesActivate,
        RatesRead, RatesUpdate, RatesBulkUpdate, RatesSetSource,
        SnapshotsRead, SnapshotsCreate, SnapshotsDelete, SnapshotsAudit,
        SystemRefreshRates, SystemRebuildCache, SystemViewStats
    };
}

/// <summary>
/// Represents a predefined role with associated permissions.
/// </summary>
public class PredefinedRole
{
    /// <summary>Gets the unique identifier for the role.</summary>
    public string RoleId { get; init; } = default!;
    /// <summary>Gets the description of the role.</summary>
    public string Description { get; init; } = default!;
    /// <summary>Gets the permissions assigned to the role.</summary>
    public string[] Permissions { get; init; } = default!;
}

/// <summary>
/// Provides access to predefined roles for the Currency Service.
/// </summary>
public static class CurrencyPredefinedRoles
{
    /// <summary>The administrator role with all permissions.</summary>
    public static readonly PredefinedRole Admin = new()
    {
        RoleId = "currency.admin",
        Description = "Currency Service Administrator",
        Permissions = CurrencyPermissions.All
    };

    /// <summary>The viewer role with read-only permissions.</summary>
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

    /// <summary>Gets all predefined role IDs.</summary>
    public static string[] AllRoleIds => new[] { Admin.RoleId, Viewer.RoleId };
    /// <summary>Gets all predefined roles.</summary>
    public static PredefinedRole[] All => new[] { Admin, Viewer };
}