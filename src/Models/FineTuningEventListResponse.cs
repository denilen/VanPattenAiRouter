namespace VanPattenAIRouter.Models;

/// <summary>
///     Represents a list of fine-tuning events response.
/// </summary>
public class FineTuningEventListResponse
{
    /// <summary>
    ///     The object type, which is always "list".
    /// </summary>
    public string Object { get; set; } = "list";

    /// <summary>
    ///     The list of events.
    /// </summary>
    public List<FineTuningEvent> Data { get; set; } = new();

    /// <summary>
    ///     Whether there are more events.
    /// </summary>
    public bool HasMore { get; set; }
}