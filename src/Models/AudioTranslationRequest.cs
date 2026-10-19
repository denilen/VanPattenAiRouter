using System.Text.Json.Serialization;

namespace VanPattenAIRouter.Models;

/// <summary>
///     Represents an audio translation request compatible with OpenAI API format.
/// </summary>
public class AudioTranslationRequest
{
    /// <summary>
    ///     The audio file to translate. Supported formats: flac, mp3, mp4, mpeg, mpga, m4a, ogg, wav, webm.
    /// </summary>
    [JsonPropertyName("file")]
    public byte[] File { get; set; } = Array.Empty<byte>();

    /// <summary>
    ///     The filename of the audio file (used to determine format).
    /// </summary>
    [JsonPropertyName("filename")]
    public string FileName { get; set; } = "audio.wav";

    /// <summary>
    ///     The model to use for translation.
    /// </summary>
    [JsonPropertyName("model")]
    public string? Model { get; set; }

    /// <summary>
    ///     An optional text to guide the model's style or continue a previous audio segment.
    /// </summary>
    [JsonPropertyName("prompt")]
    public string? Prompt { get; set; }

    /// <summary>
    ///     The format of the transcript output. Options: json, text, srt, verbose_json, vtt.
    /// </summary>
    [JsonPropertyName("response_format")]
    public string? ResponseFormat { get; set; }

    /// <summary>
    ///     The sampling temperature, between 0 and 1.
    /// </summary>
    [JsonPropertyName("temperature")]
    public double? Temperature { get; set; }
}