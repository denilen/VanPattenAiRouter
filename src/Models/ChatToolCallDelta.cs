using System.Text.Json.Serialization;

namespace VanPattenAIRouter.Models;

/// <summary>
///     Represents a tool call delta in a streaming response.
/// </summary>
public class ChatToolCallDelta
{
    /// <summary>
    ///     The index of the tool call.
    /// </summary>
    [JsonPropertyName("index")]
    public int Index { get; set; }

    /// <summary>
    ///     The ID of the tool call.
    /// </summary>
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    /// <summary>
    ///     The type of the tool.
    /// </summary>
    [JsonPropertyName("type")]
    public string? Type { get; set; }

    /// <summary>
    ///     The function call details.
    /// </summary>
    [JsonPropertyName("function")]
    public FunctionDelta? Function { get; set; }
}