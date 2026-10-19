using System.Text.Json.Serialization;

namespace VanPattenAIRouter.Models;

/// <summary>
///     Represents a function call within a tool call.
/// </summary>
public class Function
{
    /// <summary>
    ///     The name of the function to be called.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    /// <summary>
    ///     The arguments to call the function with, as a JSON string.
    /// </summary>
    [JsonPropertyName("arguments")]
    public string Arguments { get; set; } = "";
}