using System.Text.Json;
using System.Text.Json.Serialization;

namespace VanPattenAIRouter.Models;

/// <summary>
///     Represents a custom tool definition.
/// </summary>
public class CustomTool
{
    /// <summary>
    ///     The name of the custom tool.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    /// <summary>
    ///     Description of the custom tool.
    /// </summary>
    [JsonPropertyName("description")]
    public string? Description { get; set; }

    /// <summary>
    ///     The input format for the custom tool.
    /// </summary>
    [JsonPropertyName("format")]
    public JsonElement? Format { get; set; }
}