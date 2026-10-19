using System.Text.Json.Serialization;

namespace VanPattenAIRouter.Models;

/// <summary>
///     Options for streaming chat completion responses.
/// </summary>
public class ChatCompletionStreamOptions
{
    /// <summary>
    ///     If set, an additional chunk will be streamed before the data: [DONE] message.
    ///     The usage field on this chunk shows the token usage statistics for the entire request.
    /// </summary>
    [JsonPropertyName("include_usage")]
    public bool? IncludeUsage { get; set; }

    /// <summary>
    ///     If set, stream obfuscation will be enabled. Adds random characters to an obfuscation
    ///     field on streaming delta events to normalize payload sizes.
    /// </summary>
    [JsonPropertyName("include_obfuscation")]
    public bool? IncludeObfuscation { get; set; }
}