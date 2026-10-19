using System.Text.Json.Serialization;

namespace VanPattenAIRouter.Models;

/// <summary>
///     Represents a fine-tuning job event.
/// </summary>
public class FineTuningEvent
{
    /// <summary>
    ///     The unique identifier for the event.
    /// </summary>
    public string Id { get; set; } = "";

    /// <summary>
    ///     The object type, which is always "fine_tuning.job.event".
    /// </summary>
    public string Object { get; set; } = "fine_tuning.job.event";

    /// <summary>
    ///     The Unix timestamp (in seconds) when the event was created.
    /// </summary>
    public long CreatedAt { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

    /// <summary>
    ///     The fine-tuning job ID this event belongs to.
    /// </summary>
    [JsonPropertyName("fine_tuning_job_id")]
    public string FineTuningJobId { get; set; } = "";

    /// <summary>
    ///     The event message.
    /// </summary>
    public string Message { get; set; } = "";

    /// <summary>
    ///     The event level (info, warning, error).
    /// </summary>
    public string Level { get; set; } = "info";

    /// <summary>
    ///     The event type.
    /// </summary>
    public string Type { get; set; } = "";
}