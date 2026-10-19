using System.Text.Json.Serialization;

namespace VanPattenAIRouter.Models;

/// <summary>
///     Represents a batch request compatible with OpenAI API format.
/// </summary>
public class BatchRequest
{
    /// <summary>
    ///     The ID of the input file containing the requests for this batch.
    /// </summary>
    [JsonPropertyName("input_file_id")]
    public string InputFileId { get; set; } = "";

    /// <summary>
    ///     The endpoint to be used for all requests in the batch.
    /// </summary>
    [JsonPropertyName("endpoint")]
    public string Endpoint { get; set; } = "";

    /// <summary>
    ///     The time frame within which the batch should be processed.
    /// </summary>
    [JsonPropertyName("completion_window")]
    public string CompletionWindow { get; set; } = "24h";

    /// <summary>
    ///     Optional metadata for the batch.
    /// </summary>
    [JsonPropertyName("metadata")]
    public Dictionary<string, string>? Metadata { get; set; }
}