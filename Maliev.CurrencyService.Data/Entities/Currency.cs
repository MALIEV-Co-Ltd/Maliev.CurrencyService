using System.ComponentModel.DataAnnotations;

namespace Maliev.CurrencyService.Data.Entities;

public class Currency
{
    public int Id { get; set; }

    [Required]
    [MaxLength(3)]
    public required string ShortName { get; set; }

    [Required]
    [MaxLength(100)]
    public required string LongName { get; set; }

    public DateTime CreatedDate { get; set; }

    public DateTime ModifiedDate { get; set; }
}