using System.Text.Json.Serialization;

namespace VanPattenAIRouter.Models;

/// <summary>
///     Represents a user response.
/// </summary>
public class UserResponse
{
    /// <summary>
    ///     The unique identifier for the user.
    /// </summary>
    public string Id { get; set; } = "";

    /// <summary>
    ///     The object type, which is always "user".
    /// </summary>
    public string Object { get; set; } = "user";

    /// <summary>
    ///     The name of the user.
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>
    ///     The email of the user.
    /// </summary>
    public string Email { get; set; } = "";

    /// <summary>
    ///     The role of the user.
    /// </summary>
    public string Role { get; set; } = "owner";

    /// <summary>
    ///     The Unix timestamp (in seconds) when the user was added.
    /// </summary>
    [JsonPropertyName("added_at")]
    public long AddedAt { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

    /// <summary>
    ///     Whether the user is the owner.
    /// </summary>
    public bool IsOwner { get; set; } = true;
}