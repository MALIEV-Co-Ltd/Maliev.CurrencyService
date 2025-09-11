using System.ComponentModel.DataAnnotations;

namespace Maliev.CurrencyService.Api.Models;

public class GetExchangeRateRequest
{
    [Required]
    [StringLength(3, MinimumLength = 3)]
    public required string From { get; set; }

    [Required]
    public required string To { get; set; }
}