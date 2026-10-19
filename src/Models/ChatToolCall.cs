using System.Text.Json.Serialization;

namespace VanPattenAIRouter.Models;

/// <summary>
///     Represents a tool call made by the model.
/// </summary>
public class ChatToolCall
{
    /// <summary>
    ///     The ID of the tool call.
    /// </summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    /// <summary>
    ///     The type of the tool. Currently only "function" is supported.
    /// </summary>
    [JsonPropertyName("type")]
    public string Type { get; set; } = "function";

    /// <summary>
    ///     The function that the model called.
    /// </summary>
    [JsonPropertyName("function")]
    public Function Function { get; set; } = new();
}