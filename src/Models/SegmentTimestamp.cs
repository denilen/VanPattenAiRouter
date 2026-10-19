using System.Text.Json.Serialization;

namespace VanPattenAIRouter.Models;

/// <summary>
///     Represents a segment with timestamp.
/// </summary>
public class SegmentTimestamp
{
    /// <summary>
    ///     The segment text.
    /// </summary>
    [JsonPropertyName("text")]
    public string Text { get; set; } = "";

    /// <summary>
    ///     The start time in seconds.
    /// </summary>
    [JsonPropertyName("start")]
    public double Start { get; set; }

    /// <summary>
    ///     The end time in seconds.
    /// </summary>
    [JsonPropertyName("end")]
    public double End { get; set; }

    /// <summary>
    ///     The segment language.
    /// </summary>
    [JsonPropertyName("language")]
    public string Language { get; set; } = "";

    /// <summary>
    ///     The segment tokens.
    /// </summary>
    [JsonPropertyName("tokens")]
    public List<int>? Tokens { get; set; }

    /// <summary>
    ///     The segment temperature.
    /// </summary>
    [JsonPropertyName("temperature")]
    public double? Temperature { get; set; }

    /// <summary>
    ///     The segment average logprob.
    /// </summary>
    [JsonPropertyName("avg_logprob")]
    public double? AvgLogprob { get; set; }

    /// <summary>
    ///     The segment compression ratio.
    /// </summary>
    [JsonPropertyName("compression_ratio")]
    public double? CompressionRatio { get; set; }

    /// <summary>
    ///     The segment no speech probability.
    /// </summary>
    [JsonPropertyName("no_speech_prob")]
    public double? NoSpeechProb { get; set; }
}