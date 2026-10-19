using System.Text.Json.Serialization;

namespace VanPattenAIRouter.Models;

/// <summary>
///     Represents a project response.
/// </summary>
public class ProjectResponse
{
    /// <summary>
    ///     The unique identifier for the project.
    /// </summary>
    public string Id { get; set; } = "";

    /// <summary>
    ///     The object type, which is always "project".
    /// </summary>
    public string Object { get; set; } = "project";

    /// <summary>
    ///     The name of the project.
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>
    ///     The description of the project.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    ///     The Unix timestamp (in seconds) when the project was created.
    /// </summary>
    [JsonPropertyName("created_at")]
    public long CreatedAt { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

    /// <summary>
    ///     The Unix timestamp (in seconds) when the project was archived.
    /// </summary>
    [JsonPropertyName("archived_at")]
    public long? ArchivedAt { get; set; }

    /// <summary>
    ///     Whether the project is archived.
    /// </summary>
    public bool Archived { get; set; } = false;
}