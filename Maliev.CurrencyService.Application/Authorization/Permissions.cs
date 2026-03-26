namespace Maliev.CurrencyService.Application.Authorization;

/// <summary>
/// Defines the permissions for the Currency Service.
/// </summary>
public static class CurrencyPermissions
{
    public const string CurrencyRead = "currency.currencies.read";
    public const string CurrencyManage = "currency.currencies.manage";

    public const string ExchangeRateRead = "currency.exchange-rates.read";
    public const string ExchangeRateManage = "currency.exchange-rates.manage";

    public const string ConversionCalculate = "currency.conversions.calculate";

    public static readonly IReadOnlyDictionary<string, string> AllWithDescriptions = new Dictionary<string, string>
    {
        { CurrencyRead, "Read currency data" },
        { CurrencyManage, "Manage currency data" },
        { ExchangeRateRead, "Read exchange rates" },
        { ExchangeRateManage, "Manage exchange rates" },
        { ConversionCalculate, "Calculate currency conversions" },
    };

    public static string[] All => AllWithDescriptions.Keys.ToArray();
}
