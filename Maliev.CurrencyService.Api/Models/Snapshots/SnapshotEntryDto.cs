using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Maliev.CurrencyService.Api.Models.Snapshots;

/// <summary>
/// Snapshot entry DTO matching FR-026 specification
/// </summary>
/// <remarks>
/// FR-026: JSON array format with schema: [{"from":"USD","to":"EUR","rate":0.85,"timestamp":"2025-01-15T00:00:00Z"}]
/// </remarks>
public class SnapshotEntryDto
{
    /// <summary>
    /// Source currency code (ISO 4217)
    /// </summary>
    [Required]
    [StringLength(3, MinimumLength = 3)]
    [RegularExpression(@"^[A-Z]{3}$")]
    [JsonPropertyName("from")]
    public required string From { get; set; }

    /// <summary>
    /// Target currency code (ISO 4217)
    /// </summary>
    [Required]
    [StringLength(3, MinimumLength = 3)]
    [RegularExpression(@"^[A-Z]{3}$")]
    [JsonPropertyName("to")]
    public required string To { get; set; }

    /// <summary>
    /// Exchange rate (6 decimal precision per FR-SC-012)
    /// </summary>
    [Required]
    [Range(0.000001, 1000000)]
    [JsonPropertyName("rate")]
    public required decimal Rate { get; set; }

    /// <summary>
    /// Timestamp for this snapshot (ISO 8601 format string)
    /// </summary>
    /// <remarks>
    /// String type allows validation of invalid timestamps during dry-run
    /// </remarks>
    [Required]
    [JsonPropertyName("timestamp")]
    public required string Timestamp { get; set; }
}
