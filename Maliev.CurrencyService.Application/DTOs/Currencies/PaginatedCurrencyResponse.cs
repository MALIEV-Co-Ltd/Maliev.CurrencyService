using Maliev.CurrencyService.Application.DTOs.Common;

namespace Maliev.CurrencyService.Application.DTOs.Currencies;

/// <summary>
/// Paginated currency list response.
/// </summary>
/// <remarks>
/// Per contracts/openapi.yaml: Extends PaginatedResponse for currency-specific pagination.
/// </remarks>
public class PaginatedCurrencyResponse : PaginatedResponse<CurrencyResponse>
{
}
