using System.Text.Json.Serialization;

namespace Maliev.CurrencyService.Api.Models.Snapshots;

/// <summary>
/// Validation report for dry-run mode (FR-028)
/// </summary>
public class ValidationReport
{
    /// <summary>
    /// Whether the batch passed validation
    /// </summary>
    [JsonPropertyName("isValid")]
    public bool IsValid { get; init; }

    /// <summary>
    /// List of validation errors (if any)
    /// </summary>
    [JsonPropertyName("validationErrors")]
    public List<string> ValidationErrors { get; init; } = new();

    /// <summary>
    /// Number of records validated
    /// </summary>
    [JsonPropertyName("recordCount")]
    public int RecordCount { get; init; }

    /// <summary>
    /// Indicates this was a dry-run validation
    /// </summary>
    [JsonPropertyName("isDryRun")]
    public bool IsDryRun { get; init; }
}
