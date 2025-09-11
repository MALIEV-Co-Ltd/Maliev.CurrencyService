namespace Maliev.CurrencyService.Api.Models;

public class ConvertCurrencyResponse
{
    public required string FromCurrency { get; set; }
    public required string ToCurrency { get; set; }
    public decimal OriginalAmount { get; set; }
    public decimal ConvertedAmount { get; set; }
    public decimal ExchangeRate { get; set; }
    public DateTime RateTimestamp { get; set; }
    public required string Source { get; set; }
}