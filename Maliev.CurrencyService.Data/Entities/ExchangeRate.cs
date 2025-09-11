using System.ComponentModel.DataAnnotations;

namespace Maliev.CurrencyService.Data.Entities;

public class ExchangeRate
{
    public int Id { get; set; }

    [Required]
    [MaxLength(3)]
    public required string FromCurrencyCode { get; set; }

    [Required]
    [MaxLength(3)]
    public required string ToCurrencyCode { get; set; }

    [Required]
    public decimal Rate { get; set; }

    [Required]
    public DateTime FetchedAt { get; set; }

    [Required]
    [MaxLength(50)]
    public required string Source { get; set; }

    public DateTime CreatedDate { get; set; }
    public DateTime ModifiedDate { get; set; }
}