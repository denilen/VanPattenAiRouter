using System.Text.Json.Serialization;

namespace VanPattenAIRouter.Models;

/// <summary>
///     Represents audio data in a chat message.
/// </summary>
public class ChatAudio
{
    /// <summary>
    ///     Unique identifier for this audio response.
    /// </summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    /// <summary>
    ///     Base64 encoded audio bytes.
    /// </summary>
    [JsonPropertyName("data")]
    public string Data { get; set; } = "";

    /// <summary>
    ///     Unix timestamp when the audio expires.
    /// </summary>
    [JsonPropertyName("expires_at")]
    public long ExpiresAt { get; set; }

    /// <summary>
    ///     Transcript of the audio.
    /// </summary>
    [JsonPropertyName("transcript")]
    public string Transcript { get; set; } = "";
}