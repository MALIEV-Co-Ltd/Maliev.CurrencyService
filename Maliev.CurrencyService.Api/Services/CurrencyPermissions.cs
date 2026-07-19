namespace Maliev.CurrencyService.Api.Services;

/// <summary>
/// Defines granular permission constants for the Currency Service.
/// Follows GCP-style naming: {service}.{resource}.{action}
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
    /// Collection of all defined currency permissions with descriptions.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> AllWithDescriptions = new Dictionary<string, string>
    {
        { CurrenciesRead, "Read currency list and details" },
        { CurrenciesCreate, "Create new currencies" },
        { CurrenciesUpdate, "Update existing currency information" },
        { CurrenciesDelete, "Delete currencies" },
        { CurrenciesActivate, "Activate or deactivate currencies" },
        { RatesRead, "Read exchange rates" },
        { RatesUpdate, "Update individual exchange rates" },
        { RatesBulkUpdate, "Perform bulk update of exchange rates" },
        { RatesSetSource, "Set official exchange rate data source" },
        { SnapshotsRead, "Read historical currency snapshots" },
        { SnapshotsCreate, "Create historical currency snapshots" },
        { SnapshotsDelete, "Delete historical snapshots" },
        { SnapshotsAudit, "Audit currency snapshot data" },
        { SystemRefreshRates, "Manually trigger exchange rate refresh" },
        { SystemRebuildCache, "Rebuild currency system cache" },
        { SystemViewStats, "View currency service system statistics" }
    };

    /// <summary>
    /// Gets all defined permission codes.
    /// </summary>
    public static string[] All => AllWithDescriptions.Keys.ToArray();
}

/// <summary>
/// Provides access to predefined roles for the Currency Service.
/// </summary>
public static class CurrencyPredefinedRoles
{
    /// <summary>Role for administrators with full control.</summary>
    public const string Admin = "roles.currency.admin";
    /// <summary>Role for users with read-only access.</summary>
    public const string Viewer = "roles.currency.viewer";

    /// <summary>
    /// Collection of all predefined roles for the Currency Service.
    /// </summary>
    public static readonly IReadOnlyList<(string RoleId, string Description, string[] Permissions)> All = new List<(string, string, string[])>
    {
        (Admin, "Full administrative control over currencies and exchange rates", CurrencyPermissions.All),
        (Viewer, "Read-only access to currency data and exchange rates", new[]
        {
            CurrencyPermissions.CurrenciesRead,
            CurrencyPermissions.RatesRead,
            CurrencyPermissions.SnapshotsRead
        })
    };
}
