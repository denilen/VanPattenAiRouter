using System.Text.Json.Serialization;

namespace VanPattenAIRouter.Models;

/// <summary>
///     Represents a single completion choice.
/// </summary>
public class CompletionChoice
{
    /// <summary>
    ///     The generated text.
    /// </summary>
    public string Text { get; set; } = "";

    /// <summary>
    ///     The index of the choice.
    /// </summary>
    public int Index { get; set; }

    /// <summary>
    ///     The log probabilities of the tokens.
    /// </summary>
    [JsonPropertyName("logprobs")]
    public CompletionLogprobs? Logprobs { get; set; }

    /// <summary>
    ///     The reason the model stopped generating tokens.
    /// </summary>
    public string FinishReason { get; set; } = "stop";
}