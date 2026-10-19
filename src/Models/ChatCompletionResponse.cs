using System.Text.Json.Serialization;

namespace VanPattenAIRouter.Models;

/// <summary>
///     Represents a chat completion response compatible with OpenAI API format.
/// </summary>
public class ChatCompletionResponse
{
    /// <summary>
    ///     A unique identifier for the chat completion.
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    ///     The object type, which is always "chat.completion".
    /// </summary>
    public string Object { get; set; } = "chat.completion";

    /// <summary>
    ///     The Unix timestamp (in seconds) of when the chat completion was created.
    /// </summary>
    public long Created { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

    /// <summary>
    ///     The model used for the chat completion.
    /// </summary>
    public string Model { get; set; } = "";

    /// <summary>
    ///     A list of chat completion choices. Can be more than one if n is greater than 1.
    /// </summary>
    public List<ChatChoice> Choices { get; set; } = new();

    /// <summary>
    ///     Usage statistics for the completion request.
    /// </summary>
    public Usage Usage { get; set; } = new();

    /// <summary>
    ///     This fingerprint represents the backend configuration that the model runs with.
    /// </summary>
    [JsonPropertyName("system_fingerprint")]
    public string? SystemFingerprint { get; set; }

    /// <summary>
    ///     The service tier used for processing (auto, default, flex, priority).
    /// </summary>
    [JsonPropertyName("service_tier")]
    public string? ServiceTier { get; set; }
}