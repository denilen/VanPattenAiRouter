using System.Text.Json.Serialization;

namespace VanPattenAIRouter.Models;

/// <summary>
///     Detailed breakdown of completion tokens.
/// </summary>
public class CompletionTokensDetails
{
    /// <summary>
    ///     Number of reasoning tokens (for reasoning models).
    /// </summary>
    [JsonPropertyName("reasoning_tokens")]
    public int? ReasoningTokens { get; set; }

    /// <summary>
    ///     Number of audio tokens (for audio output).
    /// </summary>
    [JsonPropertyName("audio_tokens")]
    public int? AudioTokens { get; set; }

    /// <summary>
    ///     Number of accepted prediction tokens.
    /// </summary>
    [JsonPropertyName("accepted_prediction_tokens")]
    public int? AcceptedPredictionTokens { get; set; }

    /// <summary>
    ///     Number of rejected prediction tokens.
    /// </summary>
    [JsonPropertyName("rejected_prediction_tokens")]
    public int? RejectedPredictionTokens { get; set; }
}