using System.Text.Json.Serialization;

namespace VanPattenAIRouter.Models;

/// <summary>
///     Represents a word with timestamp.
/// </summary>
public class WordTimestamp
{
    /// <summary>
    ///     The word text.
    /// </summary>
    [JsonPropertyName("word")]
    public string Word { get; set; } = "";

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
}