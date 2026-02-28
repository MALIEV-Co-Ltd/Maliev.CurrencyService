using Maliev.CurrencyService.Application.DTOs.Rates;

namespace Maliev.CurrencyService.Api.Models.Rates;

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
