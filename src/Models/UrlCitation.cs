using System.Text.Json.Serialization;

namespace VanPattenAIRouter.Models;

/// <summary>
///     Represents a URL citation annotation.
/// </summary>
public class UrlCitation
{
    /// <summary>
    ///     Start index of the citation in the message.
    /// </summary>
    [JsonPropertyName("start_index")]
    public int StartIndex { get; set; }

    /// <summary>
    ///     End index of the citation in the message.
    /// </summary>
    [JsonPropertyName("end_index")]
    public int EndIndex { get; set; }

    /// <summary>
    ///     Title of the cited resource.
    /// </summary>
    [JsonPropertyName("title")]
    public string Title { get; set; } = "";

    /// <summary>
    ///     URL of the cited resource.
    /// </summary>
    [JsonPropertyName("url")]
    public string Url { get; set; } = "";
}