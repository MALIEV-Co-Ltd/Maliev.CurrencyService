using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Maliev.CurrencyService.Api.Models.Common;

/// <summary>
/// Helper class for generating consistent ETags across controllers.
/// </summary>
public static class ETagHelper
{
    /// <summary>
    /// Generates an ETag for the provided content using SHA256 hashing.
    /// </summary>
    /// <param name="content">The object content to hash.</param>
    /// <returns>A base64 encoded string of the first 16 characters of the hash.</returns>
    public static string GenerateETag(object content)
    {
        var json = JsonSerializer.Serialize(content);
        var bytes = Encoding.UTF8.GetBytes(json);
        var hash = SHA256.HashData(bytes);
        return Convert.ToBase64String(hash)[..16];
    }
}
