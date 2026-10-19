using System.Text.Json.Serialization;

namespace VanPattenAIRouter.Models;

/// <summary>
///     Represents an invite request.
/// </summary>
public class InviteRequest
{
    /// <summary>
    ///     The email to invite.
    /// </summary>
    [JsonPropertyName("email")]
    public string Email { get; set; } = "";

    /// <summary>
    ///     The role to assign to the invited user.
    /// </summary>
    [JsonPropertyName("role")]
    public string Role { get; set; } = "user";

    /// <summary>
    ///     The project ID to invite the user to.
    /// </summary>
    [JsonPropertyName("project_id")]
    public string? ProjectId { get; set; }
}