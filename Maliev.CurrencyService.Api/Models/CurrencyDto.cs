namespace Maliev.CurrencyService.Api.Models;

/// <summary>
/// Represents a currency data transfer object.
/// </summary>
public class CurrencyDto
{
    /// <summary>
    /// The unique identifier of the currency.
    /// </summary>
    public int Id { get; set; }
    /// <summary>
    /// The 3-letter ISO 4217 code for the currency (e.g., "USD").
    /// </summary>
    public required string ShortName { get; set; }
    /// <summary>
    /// The full name of the currency (e.g., "United States Dollar").
    /// </summary>
    public required string LongName { get; set; }
    /// <summary>
    /// The date and time when the currency was created.
    /// </summary>
    public DateTime CreatedDate { get; set; }
    /// <summary>
    /// The date and time when the currency was last modified.
    /// </summary>
    public DateTime ModifiedDate { get; set; }
}
