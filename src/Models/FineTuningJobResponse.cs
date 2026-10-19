using System.Text.Json.Serialization;

namespace VanPattenAIRouter.Models;

/// <summary>
///     Represents a fine-tuning job response compatible with OpenAI API format.
/// </summary>
public class FineTuningJobResponse
{
    /// <summary>
    ///     The unique identifier for the fine-tuning job.
    /// </summary>
    public string Id { get; set; } = "";

    /// <summary>
    ///     The object type, which is always "fine_tuning.job".
    /// </summary>
    public string Object { get; set; } = "fine_tuning.job";

    /// <summary>
    ///     The Unix timestamp (in seconds) when the job was created.
    /// </summary>
    public long CreatedAt { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

    /// <summary>
    ///     The name of the base model that was fine-tuned.
    /// </summary>
    public string Model { get; set; } = "";

    /// <summary>
    ///     The fine-tuned model name (available when job succeeds).
    /// </summary>
    [JsonPropertyName("fine_tuned_model")]
    public string? FineTunedModel { get; set; }

    /// <summary>
    ///     The current status of the job.
    /// </summary>
    public string Status { get; set; } = "validating";

    /// <summary>
    ///     The training file ID.
    /// </summary>
    [JsonPropertyName("training_file")]
    public string TrainingFile { get; set; } = "";

    /// <summary>
    ///     The validation file ID.
    /// </summary>
    [JsonPropertyName("validation_file")]
    public string? ValidationFile { get; set; }

    /// <summary>
    ///     The hyperparameters used for training.
    /// </summary>
    [JsonPropertyName("hyperparameters")]
    public FineTuningHyperparameters? Hyperparameters { get; set; }

    /// <summary>
    ///     The suffix for the fine-tuned model name.
    /// </summary>
    public string? Suffix { get; set; }

    /// <summary>
    ///     The organization ID.
    /// </summary>
    [JsonPropertyName("organization_id")]
    public string? OrganizationId { get; set; }

    /// <summary>
    ///     The result files (when job completes).
    /// </summary>
    [JsonPropertyName("result_files")]
    public List<string>? ResultFiles { get; set; }

    /// <summary>
    ///     The Unix timestamp when the job finished.
    /// </summary>
    [JsonPropertyName("finished_at")]
    public long? FinishedAt { get; set; }

    /// <summary>
    ///     The Unix timestamp when the job failed.
    /// </summary>
    [JsonPropertyName("failed_at")]
    public long? FailedAt { get; set; }

    /// <summary>
    ///     The error object (when job fails).
    /// </summary>
    public FineTuningError? Error { get; set; }

    /// <summary>
    ///     The trained tokens count.
    /// </summary>
    [JsonPropertyName("trained_tokens")]
    public int? TrainedTokens { get; set; }
}