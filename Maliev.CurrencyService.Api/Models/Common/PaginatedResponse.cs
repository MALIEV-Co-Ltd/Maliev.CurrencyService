namespace Maliev.CurrencyService.Api.Models.Common;

/// <summary>
/// Generic paginated response wrapper for list endpoints
/// </summary>
/// <typeparam name="T">Type of items in the response</typeparam>
/// <remarks>
/// Per contracts/openapi.yaml: Provides consistent pagination structure across all list endpoints.
/// </remarks>
public class PaginatedResponse<T>
{
    /// <summary>
    /// Items in the current page
    /// </summary>
    public required IEnumerable<T> Items { get; init; }

    /// <summary>
    /// Current page number (1-indexed)
    /// </summary>
    public required int Page { get; init; }

    /// <summary>
    /// Number of items per page
    /// </summary>
    public required int PageSize { get; init; }

    /// <summary>
    /// Total number of items across all pages
    /// </summary>
    public required int TotalCount { get; init; }

    /// <summary>
    /// Total number of pages
    /// </summary>
    public required int TotalPages { get; init; }

    /// <summary>
    /// Whether there is a next page
    /// </summary>
    public bool HasNextPage => Page < TotalPages;

    /// <summary>
    /// Whether there is a previous page
    /// </summary>
    public bool HasPreviousPage => Page > 1;
}
