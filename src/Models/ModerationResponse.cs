namespace VanPattenAIRouter.Models;

/// <summary>
///     Represents a moderation response compatible with OpenAI API format.
/// </summary>
public class ModerationResponse
{
    /// <summary>
    ///     The unique identifier for the moderation request.
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    ///     The model used for moderation.
    /// </summary>
    public string Model { get; set; } = "";

    /// <summary>
    ///     The list of moderation results.
    /// </summary>
    public List<ModerationResult> Results { get; set; } = new();
}