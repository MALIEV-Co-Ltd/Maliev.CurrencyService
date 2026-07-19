namespace Maliev.CurrencyService.Application.Authorization;

/// <summary>
/// Provides access to predefined roles for the Currency Service.
/// </summary>
public static class CurrencyPredefinedRoles
{
    public const string Admin = "roles.currency.admin";
    public const string Viewer = "roles.currency.viewer";
    public const string Analyst = "roles.currency.analyst";

    public static readonly IReadOnlyList<(string RoleId, string Description, string[] Permissions)> All = new List<(string, string, string[])>
    {
        (
            Admin,
            "Currency Administrator with full access",
            new[]
            {
                CurrencyPermissions.CurrencyRead,
                CurrencyPermissions.CurrencyManage,
                CurrencyPermissions.ExchangeRateRead,
                CurrencyPermissions.ExchangeRateManage,
                CurrencyPermissions.ConversionCalculate,
            }
        ),
        (
            Viewer,
            "Currency Viewer with read-only access",
            new[]
            {
                CurrencyPermissions.CurrencyRead,
                CurrencyPermissions.ExchangeRateRead,
                CurrencyPermissions.ConversionCalculate,
            }
        ),
        (
            Analyst,
            "Currency Analyst with rate management and conversion access",
            new[]
            {
                CurrencyPermissions.CurrencyRead,
                CurrencyPermissions.ExchangeRateRead,
                CurrencyPermissions.ExchangeRateManage,
                CurrencyPermissions.ConversionCalculate,
            }
        ),
    };
}
