namespace VanPattenAIRouter.Models;

/// <summary>
///     Represents a list of fine-tuning jobs response.
/// </summary>
public class FineTuningJobListResponse
{
    /// <summary>
    ///     The object type, which is always "list".
    /// </summary>
    public string Object { get; set; } = "list";

    /// <summary>
    ///     The list of fine-tuning jobs.
    /// </summary>
    public List<FineTuningJobResponse> Data { get; set; } = new();

    /// <summary>
    ///     Whether there are more jobs.
    /// </summary>
    public bool HasMore { get; set; }
}