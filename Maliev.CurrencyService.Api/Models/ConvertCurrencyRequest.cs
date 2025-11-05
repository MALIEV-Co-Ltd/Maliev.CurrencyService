using System.ComponentModel.DataAnnotations;

namespace Maliev.CurrencyService.Api.Models;

public class ConvertCurrencyRequest
{
    [Required]
    [StringLength(3, MinimumLength = 3)]
    public required string From { get; set; }

    [Required]
    [StringLength(3, MinimumLength = 3)]
    public required string To { get; set; }

    [Required]
    [Range(0.01, 1000000000000.00, ErrorMessage = "Amount must be greater than 0")]
    public decimal Amount { get; set; }
}