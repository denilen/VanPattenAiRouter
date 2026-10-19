using System.Text.Json.Serialization;

namespace VanPattenAIRouter.Models;

/// <summary>
///     Represents an image generation request compatible with OpenAI API format.
/// </summary>
public class ImageGenerationRequest
{
    /// <summary>
    ///     A text description of the desired image(s).
    /// </summary>
    [JsonPropertyName("prompt")]
    public string Prompt { get; set; } = "";

    /// <summary>
    ///     The model to use for image generation.
    /// </summary>
    [JsonPropertyName("model")]
    public string? Model { get; set; }

    /// <summary>
    ///     The number of images to generate. Must be between 1 and 10.
    /// </summary>
    [JsonPropertyName("n")]
    public int? N { get; set; }

    /// <summary>
    ///     The quality of the image that will be generated.
    /// </summary>
    [JsonPropertyName("quality")]
    public string? Quality { get; set; }

    /// <summary>
    ///     The size of the generated images.
    /// </summary>
    [JsonPropertyName("size")]
    public string? Size { get; set; }

    /// <summary>
    ///     The style of the generated images.
    /// </summary>
    [JsonPropertyName("style")]
    public string? Style { get; set; }

    /// <summary>
    ///     The format in which the generated images are returned.
    /// </summary>
    [JsonPropertyName("response_format")]
    public string? ResponseFormat { get; set; }

    /// <summary>
    ///     A unique identifier representing your end-user.
    /// </summary>
    [JsonPropertyName("user")]
    public string? User { get; set; }
}