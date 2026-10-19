namespace VanPattenAIRouter.Models;

/// <summary>
///     Represents a list of fine-tuning checkpoints response.
/// </summary>
public class FineTuningCheckpointListResponse
{
    /// <summary>
    ///     The object type, which is always "list".
    /// </summary>
    public string Object { get; set; } = "list";

    /// <summary>
    ///     The list of checkpoints.
    /// </summary>
    public List<FineTuningCheckpoint> Data { get; set; } = new();

    /// <summary>
    ///     Whether there are more checkpoints.
    /// </summary>
    public bool HasMore { get; set; }
}