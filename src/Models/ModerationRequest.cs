using System.Text.Json.Serialization;

namespace VanPattenAIRouter.Models;

/// <summary>
///     Represents a moderation request compatible with OpenAI API format.
/// </summary>
public class ModerationRequest
{
    /// <summary>
    ///     The input text to classify. Can be a string or array of strings.
    /// </summary>
    [JsonPropertyName("input")]
    public object Input { get; set; } = "";

    /// <summary>
    ///     The model to use for moderation.
    /// </summary>
    [JsonPropertyName("model")]
    public string? Model { get; set; }
}