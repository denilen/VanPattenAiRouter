using System.Text.Json.Serialization;

namespace VanPattenAIRouter.Models;

/// <summary>
///     Represents an audio speech request compatible with OpenAI API format.
/// </summary>
public class AudioSpeechRequest
{
    /// <summary>
    ///     The model to use for speech generation.
    /// </summary>
    [JsonPropertyName("model")]
    public string? Model { get; set; }

    /// <summary>
    ///     The text to generate audio for.
    /// </summary>
    [JsonPropertyName("input")]
    public string Input { get; set; } = "";

    /// <summary>
    ///     The voice to use for the generated audio.
    /// </summary>
    [JsonPropertyName("voice")]
    public string Voice { get; set; } = "alloy";

    /// <summary>
    ///     The format of the audio output.
    /// </summary>
    [JsonPropertyName("response_format")]
    public string? ResponseFormat { get; set; }

    /// <summary>
    ///     The speed of the generated audio. Range: 0.25 to 4.0.
    /// </summary>
    [JsonPropertyName("speed")]
    public double? Speed { get; set; }
}