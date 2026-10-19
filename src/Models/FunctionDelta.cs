using System.Text.Json.Serialization;

namespace VanPattenAIRouter.Models;

/// <summary>
///     Represents a function delta in a streaming response.
/// </summary>
public class FunctionDelta
{
    /// <summary>
    ///     The name of the function.
    /// </summary>
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    /// <summary>
    ///     The arguments (incremental).
    /// </summary>
    [JsonPropertyName("arguments")]
    public string? Arguments { get; set; }
}