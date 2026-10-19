using System.Text.Json.Serialization;

namespace VanPattenAIRouter.Models;

/// <summary>
///     Represents an organization response.
/// </summary>
public class OrganizationResponse
{
    /// <summary>
    ///     The unique identifier for the organization.
    /// </summary>
    public string Id { get; set; } = "";

    /// <summary>
    ///     The object type, which is always "organization".
    /// </summary>
    public string Object { get; set; } = "organization";

    /// <summary>
    ///     The name of the organization.
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>
    ///     The Unix timestamp (in seconds) when the organization was created.
    /// </summary>
    [JsonPropertyName("created_at")]
    public long CreatedAt { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

    /// <summary>
    ///     The default project ID for the organization.
    /// </summary>
    [JsonPropertyName("default_project_id")]
    public string? DefaultProjectId { get; set; }
}