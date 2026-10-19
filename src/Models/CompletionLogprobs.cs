using System.Text.Json.Serialization;

namespace VanPattenAIRouter.Models;

/// <summary>
///     Represents log probability information for a completion choice.
/// </summary>
public class CompletionLogprobs
{
    /// <summary>
    ///     The tokens in the completion.
    /// </summary>
    [JsonPropertyName("tokens")]
    public List<string>? Tokens { get; set; }

    /// <summary>
    ///     The log probabilities of the tokens.
    /// </summary>
    [JsonPropertyName("token_logprobs")]
    public List<double?>? TokenLogprobs { get; set; }

    /// <summary>
    ///     The top log probabilities for each token.
    /// </summary>
    [JsonPropertyName("top_logprobs")]
    public List<Dictionary<string, double>>? TopLogprobs { get; set; }

    /// <summary>
    ///     The text offset of each token.
    /// </summary>
    [JsonPropertyName("text_offset")]
    public List<int>? TextOffset { get; set; }
}