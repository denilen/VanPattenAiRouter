using System.Text.Json.Serialization;

namespace VanPattenAIRouter.Models;

/// <summary>
///     Represents a top log probability entry.
/// </summary>
public class TopLogprob
{
    /// <summary>
    ///     The token.
    /// </summary>
    [JsonPropertyName("token")]
    public string Token { get; set; } = "";

    /// <summary>
    ///     The log probability of the token.
    /// </summary>
    [JsonPropertyName("logprob")]
    public double Logprob { get; set; }

    /// <summary>
    ///     The byte representation of the token.
    /// </summary>
    [JsonPropertyName("bytes")]
    public List<int>? Bytes { get; set; }
}