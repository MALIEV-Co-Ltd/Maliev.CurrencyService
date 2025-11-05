using System.ComponentModel.DataAnnotations;

namespace Maliev.CurrencyService.Api.Models;

/// <summary>
/// Configuration options for Redis distributed cache
/// </summary>
public class RedisOptions
{
    public const string SectionName = "Redis";

    /// <summary>
    /// Redis connection string (e.g., "localhost:6379")
    /// </summary>
    [Required]
    public required string ConnectionString { get; set; }

    /// <summary>
    /// Optional instance name prefix for Redis keys
    /// </summary>
    public string? InstanceName { get; set; }

    /// <summary>
    /// Database number to use (0-15 for standard Redis)
    /// </summary>
    public int Database { get; set; } = 0;

    /// <summary>
    /// Connection timeout in seconds
    /// </summary>
    public int ConnectTimeout { get; set; } = 5;

    /// <summary>
    /// Sync timeout in milliseconds
    /// </summary>
    public int SyncTimeout { get; set; } = 5000;

    /// <summary>
    /// Whether to abort on connection failure
    /// </summary>
    public bool AbortOnConnectFail { get; set; } = false;
}
