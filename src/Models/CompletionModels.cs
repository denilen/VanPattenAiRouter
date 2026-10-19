using System.Text.Json.Serialization;

namespace VanPattenAIRouter.Models;

/// <summary>
///     Represents a text completion request compatible with OpenAI legacy API format.
/// </summary>
public class CompletionRequest
{
    /// <summary>
    ///     The model to use for completion.
    /// </summary>
    [JsonPropertyName("model")]
    public string Model { get; set; } = "";

    /// <summary>
    ///     The prompt(s) to generate completions for.
    /// </summary>
    [JsonPropertyName("prompt")]
    public object Prompt { get; set; } = "";

    /// <summary>
    ///     The suffix that comes after a completion of inserted text.
    /// </summary>
    [JsonPropertyName("suffix")]
    public string? Suffix { get; set; }

    /// <summary>
    ///     The maximum number of tokens to generate.
    /// </summary>
    [JsonPropertyName("max_tokens")]
    public int? MaxTokens { get; set; }

    /// <summary>
    ///     What sampling temperature to use.
    /// </summary>
    [JsonPropertyName("temperature")]
    public double? Temperature { get; set; }

    /// <summary>
    ///     An alternative to sampling with temperature, called nucleus sampling.
    /// </summary>
    [JsonPropertyName("top_p")]
    public double? TopP { get; set; }

    /// <summary>
    ///     How many completions to generate for each prompt.
    /// </summary>
    [JsonPropertyName("n")]
    public int? N { get; set; }

    /// <summary>
    ///     Whether to stream back partial progress.
    /// </summary>
    [JsonPropertyName("stream")]
    public bool Stream { get; set; }

    /// <summary>
    ///     Include the log probabilities on the most likely tokens.
    /// </summary>
    [JsonPropertyName("logprobs")]
    public int? Logprobs { get; set; }

    /// <summary>
    ///     Echo back the prompt in addition to the completion.
    /// </summary>
    [JsonPropertyName("echo")]
    public bool? Echo { get; set; }

    /// <summary>
    ///     Up to 4 sequences where the API will stop generating further tokens.
    /// </summary>
    [JsonPropertyName("stop")]
    public object? Stop { get; set; }

    /// <summary>
    ///     Number between -2.0 and 2.0. Positive values penalize new tokens based on frequency.
    /// </summary>
    [JsonPropertyName("presence_penalty")]
    public double? PresencePenalty { get; set; }

    /// <summary>
    ///     Number between -2.0 and 2.0. Positive values penalize new tokens based on presence.
    /// </summary>
    [JsonPropertyName("frequency_penalty")]
    public double? FrequencyPenalty { get; set; }

    /// <summary>
    ///     Generates best_of completions server-side and returns the "best".
    /// </summary>
    [JsonPropertyName("best_of")]
    public int? BestOf { get; set; }

    /// <summary>
    ///     Modify the likelihood of specified tokens appearing in the completion.
    /// </summary>
    [JsonPropertyName("logit_bias")]
    public Dictionary<int, double>? LogitBias { get; set; }

    /// <summary>
    ///     A unique identifier representing your end-user.
    /// </summary>
    [JsonPropertyName("user")]
    public string? User { get; set; }
}