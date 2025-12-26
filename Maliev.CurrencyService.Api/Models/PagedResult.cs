namespace Maliev.CurrencyService.Api.Models;

/// <summary>
/// Represents a paginated result set of items.
/// </summary>
/// <typeparam name="T">The type of items in the result set.</typeparam>
public class PagedResult<T>
{
    /// <summary>
    /// The collection of items for the current page.
    /// </summary>
    public IEnumerable<T> Items { get; set; } = new List<T>();
    /// <summary>
    /// The total count of items across all pages.
    /// </summary>
    public int TotalCount { get; set; }
    /// <summary>
    /// The current page number (1-indexed).
    /// </summary>
    public int Page { get; set; }
    /// <summary>
    /// The number of items per page.
    /// </summary>
    public int PageSize { get; set; }
    /// <summary>
    /// The total number of pages.
    /// </summary>
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
    /// <summary>
    /// Indicates whether there is a next page.
    /// </summary>
    public bool HasNextPage => Page < TotalPages;
    /// <summary>
    /// Indicates whether there is a previous page.
    /// </summary>
    public bool HasPreviousPage => Page > 1;
}
