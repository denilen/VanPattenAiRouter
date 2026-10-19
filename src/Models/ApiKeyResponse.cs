using System.Text.Json.Serialization;

namespace VanPattenAIRouter.Models;

/// <summary>
///     Represents an API key response.
/// </summary>
public class ApiKeyResponse
{
    /// <summary>
    ///     The unique identifier for the API key.
    /// </summary>
    public string Id { get; set; } = "";

    /// <summary>
    ///     The object type, which is always "api_key".
    /// </summary>
    public string Object { get; set; } = "api_key";

    /// <summary>
    ///     The name of the API key.
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>
    ///     The role of the API key.
    /// </summary>
    public string Role { get; set; } = "user";

    /// <summary>
    ///     The API key value (only returned on creation).
    /// </summary>
    [JsonPropertyName("key")]
    public string? Key { get; set; }

    /// <summary>
    ///     The Unix timestamp (in seconds) when the API key was created.
    /// </summary>
    [JsonPropertyName("created_at")]
    public long CreatedAt { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

    /// <summary>
    ///     The Unix timestamp (in seconds) when the API key expires.
    /// </summary>
    [JsonPropertyName("expires_at")]
    public long? ExpiresAt { get; set; }

    /// <summary>
    ///     Whether the API key has been revoked.
    /// </summary>
    public bool Revoked { get; set; } = false;
}