using System.Text.Json.Serialization;

namespace VanPattenAIRouter.Models;

/// <summary>
///     Configuration for predicted output (speculative decoding).
/// </summary>
public class ChatCompletionPrediction
{
    /// <summary>
    ///     The type of the predicted content. Always "content".
    /// </summary>
    [JsonPropertyName("type")]
    public string Type { get; set; } = "content";

    /// <summary>
    ///     The content that should be matched when generating a model response.
    /// </summary>
    [JsonPropertyName("content")]
    public object? Content { get; set; }
}