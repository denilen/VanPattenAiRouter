namespace VanPattenAIRouter.Models;

/// <summary>
///     Represents a single moderation result.
/// </summary>
public class ModerationResult
{
    /// <summary>
    ///     Whether the content has been flagged.
    /// </summary>
    public bool Flagged { get; set; }

    /// <summary>
    ///     The categories that were flagged.
    /// </summary>
    public ModerationCategories Categories { get; set; } = new();

    /// <summary>
    ///     The category scores.
    /// </summary>
    public ModerationCategoryScores CategoryScores { get; set; } = new();
}