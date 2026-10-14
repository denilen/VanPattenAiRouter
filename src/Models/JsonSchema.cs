using System.Text.Json;
using System.Text.Json.Serialization;

namespace VanPattenAIRouter.Models;

/// <summary>
///     Represents a JSON schema for structured output.
/// </summary>
public class JsonSchema
{
    /// <summary>
    ///     The name of the schema.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    /// <summary>
    ///     The JSON schema definition.
    /// </summary>
    [JsonPropertyName("schema")]
    public JsonElement Schema { get; set; }

    /// <summary>
    ///     Whether the schema is strict.
    /// </summary>
    [JsonPropertyName("strict")]
    public bool? Strict { get; set; }
}