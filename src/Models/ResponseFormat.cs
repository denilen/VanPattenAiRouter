using System.Text.Json.Serialization;

namespace VanPattenAIRouter.Models;

/// <summary>
///     Represents the response format configuration for structured outputs.
/// </summary>
public class ResponseFormat
{
    /// <summary>
    ///     The type of response format. Can be "text", "json_object", or "json_schema".
    /// </summary>
    [JsonPropertyName("type")]
    public string Type { get; set; } = "text";

    /// <summary>
    ///     The JSON schema for structured output (when type is "json_schema").
    /// </summary>
    [JsonPropertyName("json_schema")]
    public JsonSchema? JsonSchema { get; set; }
}