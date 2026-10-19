using System.Text.Json.Serialization;

namespace VanPattenAIRouter.Models;

/// <summary>
///     Represents an image variation request compatible with OpenAI API format.
/// </summary>
public class ImageVariationRequest
{
    /// <summary>
    ///     The image to use as a basis for the variation(s). Must be a valid PNG file, less than 4MB, and square.
    /// </summary>
    [JsonPropertyName("image")]
    public byte[] Image { get; set; } = Array.Empty<byte>();

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
    ///     The size of the generated images.
    /// </summary>
    [JsonPropertyName("size")]
    public string? Size { get; set; }

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