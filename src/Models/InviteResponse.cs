using System.Text.Json.Serialization;

namespace VanPattenAIRouter.Models;

/// <summary>
///     Represents an invite response.
/// </summary>
public class InviteResponse
{
    /// <summary>
    ///     The unique identifier for the invite.
    /// </summary>
    public string Id { get; set; } = "";

    /// <summary>
    ///     The object type, which is always "invite".
    /// </summary>
    public string Object { get; set; } = "invite";

    /// <summary>
    ///     The email of the invited user.
    /// </summary>
    public string Email { get; set; } = "";

    /// <summary>
    ///     The role assigned to the invited user.
    /// </summary>
    public string Role { get; set; } = "user";

    /// <summary>
    ///     The project ID the invite is for.
    /// </summary>
    [JsonPropertyName("project_id")]
    public string? ProjectId { get; set; }

    /// <summary>
    ///     The Unix timestamp (in seconds) when the invite was created.
    /// </summary>
    [JsonPropertyName("created_at")]
    public long CreatedAt { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

    /// <summary>
    ///     The Unix timestamp (in seconds) when the invite expires.
    /// </summary>
    [JsonPropertyName("expires_at")]
    public long? ExpiresAt { get; set; }

    /// <summary>
    ///     The status of the invite (pending, accepted, expired).
    /// </summary>
    public string Status { get; set; } = "pending";
}