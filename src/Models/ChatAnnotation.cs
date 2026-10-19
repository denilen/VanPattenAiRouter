using System.Text.Json.Serialization;

namespace VanPattenAIRouter.Models;

/// <summary>
///     Represents an annotation in a chat message (e.g., URL citation).
/// </summary>
public class ChatAnnotation
{
    /// <summary>
    ///     The type of annotation (always "url_citation").
    /// </summary>
    [JsonPropertyName("type")]
    public string Type { get; set; } = "url_citation";

    /// <summary>
    ///     The URL citation details.
    /// </summary>
    [JsonPropertyName("url_citation")]
    public UrlCitation? UrlCitation { get; set; }
}