using System.Text.Json.Serialization;

namespace VanPattenAIRouter.Models;

/// <summary>
///     Represents moderation category scores.
/// </summary>
public class ModerationCategoryScores
{
    /// <summary>
    ///     Sexual content score.
    /// </summary>
    public double Sexual { get; set; }

    /// <summary>
    ///     Hate speech score.
    /// </summary>
    public double Hate { get; set; }

    /// <summary>
    ///     Harassment score.
    /// </summary>
    public double Harassment { get; set; }

    /// <summary>
    ///     Self-harm score.
    /// </summary>
    public double SelfHarm { get; set; }

    /// <summary>
    ///     Sexual/minors score.
    /// </summary>
    [JsonPropertyName("sexual/minors")]
    public double SexualMinors { get; set; }

    /// <summary>
    ///     Hate/threatening score.
    /// </summary>
    [JsonPropertyName("hate/threatening")]
    public double HateThreatening { get; set; }

    /// <summary>
    ///     Violence/graphic score.
    /// </summary>
    [JsonPropertyName("violence/graphic")]
    public double ViolenceGraphic { get; set; }

    /// <summary>
    ///     Self-harm/intent score.
    /// </summary>
    [JsonPropertyName("self-harm/intent")]
    public double SelfHarmIntent { get; set; }

    /// <summary>
    ///     Self-harm/instructions score.
    /// </summary>
    [JsonPropertyName("self-harm/instructions")]
    public double SelfHarmInstructions { get; set; }

    /// <summary>
    ///     Harassment/threatening score.
    /// </summary>
    [JsonPropertyName("harassment/threatening")]
    public double HarassmentThreatening { get; set; }

    /// <summary>
    ///     Violence score.
    /// </summary>
    public double Violence { get; set; }
}