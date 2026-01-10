namespace Maliev.CurrencyService.Api.Models.Rates;

/// <summary>Request to update a single exchange rate.</summary>
public class UpdateRateRequest
{
    /// <summary>The source currency code.</summary>
    public required string From { get; set; }
    /// <summary>The target currency code.</summary>
    public required string To { get; set; }
    /// <summary>The new exchange rate value.</summary>
    public required decimal Rate { get; set; }
}

/// <summary>Request to update multiple exchange rates in a single batch.</summary>
public class BulkUpdateRatesRequest
{
    /// <summary>The list of rates to update.</summary>
    public required List<UpdateRateRequest> Rates { get; set; }
}

/// <summary>Request to set the active exchange rate source.</summary>
public class SetRateSourceRequest
{
    /// <summary>The name of the provider to set as primary.</summary>
    public required string ProviderName { get; set; }
}
