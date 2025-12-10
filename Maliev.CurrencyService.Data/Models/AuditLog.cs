using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Maliev.CurrencyService.Data.Models;

/// <summary>
/// Audit log entity for tracking data changes
/// </summary>
[Table("audit_logs")]
public class AuditLog
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("entity_type")]
    [Required]
    [MaxLength(100)]
    public string EntityType { get; set; } = string.Empty;

    [Column("entity_id")]
    [Required]
    [MaxLength(100)]
    public string EntityId { get; set; } = string.Empty;

    [Column("operation")]
    [Required]
    [MaxLength(20)]
    public string Operation { get; set; } = string.Empty;

    [Column("changed_fields")]
    public string? ChangedFields { get; set; }

    [Column("timestamp")]
    public DateTime Timestamp { get; set; }

    [Column("user_id")]
    [MaxLength(100)]
    public string? UserId { get; set; }
}
