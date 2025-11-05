using Maliev.CurrencyService.Api.Models.Common;

namespace Maliev.CurrencyService.Api.Models.Currencies;

/// <summary>
/// Paginated currency list response
/// </summary>
/// <remarks>
/// Per contracts/openapi.yaml: Extends PaginatedResponse for currency-specific pagination.
/// </remarks>
public class PaginatedCurrencyResponse : PaginatedResponse<CurrencyResponse>
{
}
