using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Maliev.CurrencyService.Domain.Entities;

/// <summary>
/// Entity for tracking the status of bulk operations (e.g., snapshot ingestion batches).
/// </summary>
[Table("batch_statuses")]
public class BatchStatus
{
    /// <summary>Gets or sets the unique identifier.</summary>
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    /// <summary>Gets or sets the string batch identifier (GUID format).</summary>
    [Column("batch_id")]
    [Required]
    public string BatchId { get; set; } = string.Empty;

    /// <summary>Gets or sets the current status of the batch operation.</summary>
    [Column("status")]
    [Required]
    [MaxLength(256)]
    public string Status { get; set; } = "Queued";

    /// <summary>Gets or sets the error message if the batch failed, otherwise null.</summary>
    [Column("error_message")]
    public string? ErrorMessage { get; set; }

    /// <summary>Gets or sets the UTC timestamp when this batch status record was created.</summary>
    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    /// <summary>Gets or sets the UTC timestamp when this batch status record was last updated.</summary>
    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; }
}
