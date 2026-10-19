using System.Text.Json.Serialization;

namespace VanPattenAIRouter.Models;

/// <summary>
///     Represents a batch response compatible with OpenAI API format.
/// </summary>
public class BatchResponse
{
    /// <summary>
    ///     The unique identifier for the batch.
    /// </summary>
    public string Id { get; set; } = "";

    /// <summary>
    ///     The object type, which is always "batch".
    /// </summary>
    public string Object { get; set; } = "batch";

    /// <summary>
    ///     The endpoint used for all requests in the batch.
    /// </summary>
    [JsonPropertyName("endpoint")]
    public string Endpoint { get; set; } = "";

    /// <summary>
    ///     The time frame within which the batch should be processed.
    /// </summary>
    [JsonPropertyName("completion_window")]
    public string CompletionWindow { get; set; } = "24h";

    /// <summary>
    ///     The current status of the batch.
    /// </summary>
    public string Status { get; set; } = "validating";

    /// <summary>
    ///     The ID of the input file for the batch.
    /// </summary>
    [JsonPropertyName("input_file_id")]
    public string InputFileId { get; set; } = "";

    /// <summary>
    ///     The ID of the output file containing the results (when completed).
    /// </summary>
    [JsonPropertyName("output_file_id")]
    public string? OutputFileId { get; set; }

    /// <summary>
    ///     The ID of the error file containing any errors (when failed).
    /// </summary>
    [JsonPropertyName("error_file_id")]
    public string? ErrorFileId { get; set; }

    /// <summary>
    ///     Any errors that occurred during batch processing.
    /// </summary>
    public BatchErrors? Errors { get; set; }

    /// <summary>
    ///     The Unix timestamp (in seconds) when the batch was created.
    /// </summary>
    public long CreatedAt { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

    /// <summary>
    ///     The Unix timestamp (in seconds) when the batch started processing.
    /// </summary>
    [JsonPropertyName("in_progress_at")]
    public long? InProgressAt { get; set; }

    /// <summary>
    ///     The Unix timestamp (in seconds) when the batch completed successfully.
    /// </summary>
    [JsonPropertyName("completed_at")]
    public long? CompletedAt { get; set; }

    /// <summary>
    ///     The Unix timestamp (in seconds) when the batch failed.
    /// </summary>
    [JsonPropertyName("failed_at")]
    public long? FailedAt { get; set; }

    /// <summary>
    ///     The Unix timestamp (in seconds) when the batch was cancelled.
    /// </summary>
    [JsonPropertyName("cancelled_at")]
    public long? CancelledAt { get; set; }

    /// <summary>
    ///     The Unix timestamp (in seconds) when the batch expires.
    /// </summary>
    [JsonPropertyName("expires_at")]
    public long? ExpiresAt { get; set; }

    /// <summary>
    ///     Metrics about the batch processing.
    /// </summary>
    public BatchMetrics? Metrics { get; set; }

    /// <summary>
    ///     Optional metadata for the batch.
    /// </summary>
    public Dictionary<string, string>? Metadata { get; set; }
}