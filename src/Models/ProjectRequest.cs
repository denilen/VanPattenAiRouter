using System.Text.Json.Serialization;

namespace VanPattenAIRouter.Models;

/// <summary>
///     Represents a project request.
/// </summary>
public class ProjectRequest
{
    /// <summary>
    ///     The name of the project.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    /// <summary>
    ///     The description of the project.
    /// </summary>
    [JsonPropertyName("description")]
    public string? Description { get; set; }
}