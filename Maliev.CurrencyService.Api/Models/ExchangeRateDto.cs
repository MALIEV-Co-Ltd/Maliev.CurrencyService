namespace Maliev.CurrencyService.Api.Models;

public class ExchangeRateDto
{
    public required string FromCurrency { get; set; }
    public required string ToCurrency { get; set; }
    public decimal Rate { get; set; }
    public DateTime FetchedAt { get; set; }
    public required string Source { get; set; }
}