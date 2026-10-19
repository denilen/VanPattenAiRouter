using System.Text.Json.Serialization;

namespace VanPattenAIRouter.Models;

/// <summary>
///     Represents a single generated image.
/// </summary>
public class ImageData
{
    /// <summary>
    ///     The URL of the generated image.
    /// </summary>
    [JsonPropertyName("url")]
    public string? Url { get; set; }

    /// <summary>
    ///     The base64-encoded JSON of the generated image.
    /// </summary>
    [JsonPropertyName("b64_json")]
    public string? B64Json { get; set; }

    /// <summary>
    ///     The prompt that was used to generate the image (revised by the model).
    /// </summary>
    [JsonPropertyName("revised_prompt")]
    public string? RevisedPrompt { get; set; }
}