using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Maliev.CurrencyService.Domain.Entities;

/// <summary>
/// Audit log entity for tracking data changes to domain entities.
/// </summary>
[Table("audit_logs")]
public class AuditLog
{
    /// <summary>Gets or sets the unique identifier.</summary>
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    /// <summary>Gets or sets the type name of the entity that was changed.</summary>
    [Column("entity_type")]
    [Required]
    [MaxLength(100)]
    public string EntityType { get; set; } = string.Empty;

    /// <summary>Gets or sets the string representation of the entity's primary key.</summary>
    [Column("entity_id")]
    [Required]
    [MaxLength(100)]
    public string EntityId { get; set; } = string.Empty;

    /// <summary>Gets or sets the operation performed (Added, Modified, Deleted).</summary>
    [Column("operation")]
    [Required]
    [MaxLength(20)]
    public string Operation { get; set; } = string.Empty;

    /// <summary>Gets or sets the JSON-serialized list of changed fields.</summary>
    [Column("changed_fields")]
    public string? ChangedFields { get; set; }

    /// <summary>Gets or sets the UTC timestamp of the audit event.</summary>
    [Column("timestamp")]
    public DateTime Timestamp { get; set; }

    /// <summary>Gets or sets the identifier of the user who made the change.</summary>
    [Column("user_id")]
    [MaxLength(100)]
    public string? UserId { get; set; }
}
