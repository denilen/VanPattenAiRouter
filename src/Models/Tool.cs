using System.Text.Json.Serialization;

namespace VanPattenAIRouter.Models;

/// <summary>
///     Represents a tool (function or custom) that the model can call.
/// </summary>
public class Tool
{
    /// <summary>
    ///     The type of the tool. "function" or "custom".
    /// </summary>
    [JsonPropertyName("type")]
    public string Type { get; set; } = "function";

    /// <summary>
    ///     The function definition (when type is "function").
    /// </summary>
    [JsonPropertyName("function")]
    public FunctionDefinition? Function { get; set; }

    /// <summary>
    ///     The custom tool definition (when type is "custom").
    /// </summary>
    [JsonPropertyName("custom")]
    public CustomTool? Custom { get; set; }
}