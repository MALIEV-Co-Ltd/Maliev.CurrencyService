using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Maliev.CurrencyService.Data.Models;

/// <summary>
/// Entity for tracking the status of bulk operations (e.g., snapshot ingestion)
/// </summary>
[Table("batch_statuses")]
public class BatchStatus
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("batch_id")]
    [Required]
    public string BatchId { get; set; } = string.Empty;

    [Column("status")]
    [Required]
    [MaxLength(256)]
    public string Status { get; set; } = "Queued";

    [Column("error_message")]
    public string? ErrorMessage { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; }
}
