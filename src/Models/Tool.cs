using System.Text.Json.Serialization;

namespace VanPattenAIRouter.Models;

/// <summary>
///     Represents a tool (function) that the model can call.
/// </summary>
public class Tool
{
    /// <summary>
    ///     The type of the tool. Currently only "function" is supported.
    /// </summary>
    [JsonPropertyName("type")]
    public string Type { get; set; } = "function";

    /// <summary>
    ///     The function definition.
    /// </summary>
    [JsonPropertyName("function")]
    public FunctionDefinition Function { get; set; } = new();
}