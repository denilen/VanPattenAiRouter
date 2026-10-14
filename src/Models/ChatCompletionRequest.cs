using System.Text.Json.Serialization;

namespace VanPattenAIRouter.Models;

/// <summary>
///     Represents a chat completion request compatible with OpenAI API format.
/// </summary>
public class ChatCompletionRequest
{
    /// <summary>
    ///     ID of the model to use. See the model endpoint compatibility table for details on which models work with the Chat
    ///     API.
    /// </summary>
    [JsonPropertyName("model")]
    public string Model { get; set; } = "";

    /// <summary>
    ///     A list of messages comprising the conversation so far.
    /// </summary>
    [JsonPropertyName("messages")]
    public List<ChatMessage> Messages { get; set; } = new();

    /// <summary>
    ///     What sampling temperature to use, between 0 and 2. Higher values like 0.8 will make the output more random, while
    ///     lower values like 0.2 will make it more focused and deterministic.
    /// </summary>
    [JsonPropertyName("temperature")]
    public double? Temperature { get; set; }

    /// <summary>
    ///     The maximum number of tokens that can be generated in the chat completion.
    /// </summary>
    [JsonPropertyName("max_tokens")]
    public int? MaxTokens { get; set; }

    /// <summary>
    ///     An alternative to sampling with temperature, called nucleus sampling, where the model considers the results of the
    ///     tokens with top_p probability mass.
    /// </summary>
    [JsonPropertyName("top_p")]
    public double? TopP { get; set; }

    /// <summary>
    ///     How many chat completion choices to generate for each input message.
    /// </summary>
    [JsonPropertyName("n")]
    public int? N { get; set; }

    /// <summary>
    ///     If set, partial message deltas will be sent, like in ChatGPT. Tokens will be sent as data-only server-sent events
    ///     as they become available.
    /// </summary>
    [JsonPropertyName("stream")]
    public bool Stream { get; set; }

    /// <summary>
    ///     Up to 4 sequences where the API will stop generating further tokens.
    /// </summary>
    [JsonPropertyName("stop")]
    public List<string>? Stop { get; set; }

    /// <summary>
    ///     An object specifying the format that the model must output.
    /// </summary>
    [JsonPropertyName("response_format")]
    public ResponseFormat? ResponseFormat { get; set; }

    /// <summary>
    ///     A list of tools the model may call. Currently, only functions are supported as a tool.
    /// </summary>
    [JsonPropertyName("tools")]
    public List<Tool>? Tools { get; set; }

    /// <summary>
    ///     Controls which (if any) function is called by the model.
    /// </summary>
    [JsonPropertyName("tool_choice")]
    public object? ToolChoice { get; set; }

    /// <summary>
    ///     Whether to return log probabilities of the output tokens or not.
    /// </summary>
    [JsonPropertyName("logprobs")]
    public bool? Logprobs { get; set; }

    /// <summary>
    ///     An integer between 0 and 5 specifying the number of most likely tokens to return at each token position, each with
    ///     an associated log probability.
    /// </summary>
    [JsonPropertyName("top_logprobs")]
    public int? TopLogprobs { get; set; }

    /// <summary>
    ///     A unique identifier representing your end-user, which can help monitoring and detecting abuse.
    /// </summary>
    [JsonPropertyName("user")]
    public string? User { get; set; }
}