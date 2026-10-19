using System.Text.Json.Serialization;

namespace VanPattenAIRouter.Models;

/// <summary>
///     Represents moderation categories.
/// </summary>
public class ModerationCategories
{
    /// <summary>
    ///     Sexual content.
    /// </summary>
    public bool Sexual { get; set; }

    /// <summary>
    ///     Hate speech.
    /// </summary>
    public bool Hate { get; set; }

    /// <summary>
    ///     Harassment.
    /// </summary>
    public bool Harassment { get; set; }

    /// <summary>
    ///     Self-harm.
    /// </summary>
    public bool SelfHarm { get; set; }

    /// <summary>
    ///     Sexual/minors.
    /// </summary>
    [JsonPropertyName("sexual/minors")]
    public bool SexualMinors { get; set; }

    /// <summary>
    ///     Hate/threatening.
    /// </summary>
    [JsonPropertyName("hate/threatening")]
    public bool HateThreatening { get; set; }

    /// <summary>
    ///     Violence/graphic.
    /// </summary>
    [JsonPropertyName("violence/graphic")]
    public bool ViolenceGraphic { get; set; }

    /// <summary>
    ///     Self-harm/intent.
    /// </summary>
    [JsonPropertyName("self-harm/intent")]
    public bool SelfHarmIntent { get; set; }

    /// <summary>
    ///     Self-harm/instructions.
    /// </summary>
    [JsonPropertyName("self-harm/instructions")]
    public bool SelfHarmInstructions { get; set; }

    /// <summary>
    ///     Harassment/threatening.
    /// </summary>
    [JsonPropertyName("harassment/threatening")]
    public bool HarassmentThreatening { get; set; }

    /// <summary>
    ///     Violence.
    /// </summary>
    public bool Violence { get; set; }
}