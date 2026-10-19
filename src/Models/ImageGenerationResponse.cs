using System.Text.Json.Serialization;

namespace VanPattenAIRouter.Models;

/// <summary>
///     Represents an image generation response compatible with OpenAI API format.
/// </summary>
public class ImageGenerationResponse
{
    /// <summary>
    ///     The Unix timestamp (in seconds) of when the images were created.
    /// </summary>
    [JsonPropertyName("created")]
    public long Created { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

    /// <summary>
    ///     The list of generated images.
    /// </summary>
    [JsonPropertyName("data")]
    public List<ImageData> Data { get; set; } = new();
}