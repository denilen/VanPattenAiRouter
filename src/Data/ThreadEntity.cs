using System.Text.Json;
using VanPattenAIRouter.Models;

namespace VanPattenAIRouter.Data;

/// <summary>
///     Represents a thread entity stored in the database.
/// </summary>
public class ThreadEntity
{
    /// <summary>
    ///     Unique identifier for the thread (OpenAI format: thread_...).
    /// </summary>
    public string Id { get; set; } = "";

    /// <summary>
    ///     Set of 16 key-value pairs that can be attached to an object (JSON).
    /// </summary>
    public string MetadataJson { get; set; } = "{}";

    /// <summary>
    ///     Unix timestamp (in seconds) when the thread was created.
    /// </summary>
    public long CreatedAt { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

    /// <summary>
    ///     Converts to ThreadResponse for API response.
    /// </summary>
    public ThreadResponse ToThreadResponse()
    {
        return new ThreadResponse
        {
            Id = Id,
            Object = "thread",
            CreatedAt = CreatedAt,
            Metadata = string.IsNullOrEmpty(MetadataJson)
                ? new Dictionary<string, string>()
                : JsonSerializer.Deserialize<Dictionary<string, string>>(MetadataJson)
                ?? new Dictionary<string, string>()
        };
    }
}