using System.Text.Json.Serialization;

namespace VanPattenAIRouter.Models;

/// <summary>
///     Represents log probability information for a choice.
/// </summary>
public class ChatLogprobs
{
    /// <summary>
    ///     Log probabilities for content tokens.
    /// </summary>
    [JsonPropertyName("content")]
    public List<TokenLogprob>? Content { get; set; }

    /// <summary>
    ///     Log probabilities for refusal tokens.
    /// </summary>
    [JsonPropertyName("refusal")]
    public List<TokenLogprob>? Refusal { get; set; }
}