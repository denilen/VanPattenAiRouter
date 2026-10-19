using System.Text.Json.Serialization;

namespace VanPattenAIRouter.Models;

/// <summary>
///     Represents an audio transcription response compatible with OpenAI API format.
/// </summary>
public class AudioTranscriptionResponse
{
    /// <summary>
    ///     The transcribed text.
    /// </summary>
    [JsonPropertyName("text")]
    public string Text { get; set; } = "";

    /// <summary>
    ///     The language of the transcription (when response_format is verbose_json).
    /// </summary>
    [JsonPropertyName("language")]
    public string? Language { get; set; }

    /// <summary>
    ///     The duration of the audio (when response_format is verbose_json).
    /// </summary>
    [JsonPropertyName("duration")]
    public double? Duration { get; set; }

    /// <summary>
    ///     The words with timestamps (when response_format is verbose_json).
    /// </summary>
    [JsonPropertyName("words")]
    public List<WordTimestamp>? Words { get; set; }

    /// <summary>
    ///     The segments with timestamps (when response_format is verbose_json).
    /// </summary>
    [JsonPropertyName("segments")]
    public List<SegmentTimestamp>? Segments { get; set; }
}