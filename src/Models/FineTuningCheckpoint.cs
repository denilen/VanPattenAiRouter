using System.Text.Json.Serialization;

namespace VanPattenAIRouter.Models;

/// <summary>
///     Represents a fine-tuning checkpoint.
/// </summary>
public class FineTuningCheckpoint
{
    /// <summary>
    ///     The checkpoint ID.
    /// </summary>
    public string Id { get; set; } = "";

    /// <summary>
    ///     The object type, which is always "fine_tuning.checkpoint".
    /// </summary>
    public string Object { get; set; } = "fine_tuning.checkpoint";

    /// <summary>
    ///     The fine-tuning job ID this checkpoint belongs to.
    /// </summary>
    [JsonPropertyName("fine_tuning_job_id")]
    public string FineTuningJobId { get; set; } = "";

    /// <summary>
    ///     The step number of the checkpoint.
    /// </summary>
    public int Step { get; set; }

    /// <summary>
    ///     The metrics at this checkpoint.
    /// </summary>
    public FineTuningCheckpointMetrics? Metrics { get; set; }

    /// <summary>
    ///     The fine-tuned model name at this checkpoint.
    /// </summary>
    [JsonPropertyName("fine_tuned_model")]
    public string? FineTunedModel { get; set; }

    /// <summary>
    ///     The Unix timestamp (in seconds) when the checkpoint was created.
    /// </summary>
    public long CreatedAt { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
}