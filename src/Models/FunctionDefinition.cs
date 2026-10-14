using System.Text.Json;
using System.Text.Json.Serialization;

namespace VanPattenAIRouter.Models;

/// <summary>
///     Represents a function definition for tool calling.
/// </summary>
public class FunctionDefinition
{
    /// <summary>
    ///     The name of the function.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    /// <summary>
    ///     A description of what the function does.
    /// </summary>
    [JsonPropertyName("description")]
    public string? Description { get; set; }

    /// <summary>
    ///     The parameters schema for the function.
    /// </summary>
    [JsonPropertyName("parameters")]
    public JsonElement Parameters { get; set; }

    /// <summary>
    ///     Whether the function parameters schema is strict.
    /// </summary>
    [JsonPropertyName("strict")]
    public bool? Strict { get; set; }
}