using System.ComponentModel.DataAnnotations;

namespace Maliev.CurrencyService.Api.Models;

public class UpdateCurrencyRequest
{
    [Required]
    [StringLength(3, MinimumLength = 3)]
    [RegularExpression(@"^[A-Z]{3}$", ErrorMessage = "ShortName must be exactly 3 uppercase letters")]
    public required string ShortName { get; set; }

    [Required]
    [StringLength(100, MinimumLength = 1)]
    public required string LongName { get; set; }
}