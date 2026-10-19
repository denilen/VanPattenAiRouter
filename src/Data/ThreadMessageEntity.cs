using System.Text.Json;
using VanPattenAIRouter.Models;

namespace VanPattenAIRouter.Data;

/// <summary>
///     Represents a thread message entity stored in the database.
/// </summary>
public class ThreadMessageEntity
{
    /// <summary>
    ///     Unique identifier for the message (OpenAI format: msg_...).
    /// </summary>
    public string Id { get; set; } = "";

    /// <summary>
    ///     The thread ID this message belongs to.
    /// </summary>
    public string ThreadId { get; set; } = "";

    /// <summary>
    ///     The role of the message (user or assistant).
    /// </summary>
    public string Role { get; set; } = "user";

    /// <summary>
    ///     The content of the message (JSON).
    /// </summary>
    public string ContentJson { get; set; } = "";

    /// <summary>
    ///     The file IDs attached to the message (JSON).
    /// </summary>
    public string FileIdsJson { get; set; } = "[]";

    /// <summary>
    ///     Set of 16 key-value pairs that can be attached to an object (JSON).
    /// </summary>
    public string MetadataJson { get; set; } = "{}";

    /// <summary>
    ///     The assistant ID that created this message (if assistant message).
    /// </summary>
    public string? AssistantId { get; set; }

    /// <summary>
    ///     The run ID that created this message (if assistant message).
    /// </summary>
    public string? RunId { get; set; }

    /// <summary>
    ///     Unix timestamp (in seconds) when the message was created.
    /// </summary>
    public long CreatedAt { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

    /// <summary>
    ///     Converts to MessageResponse for API response.
    /// </summary>
    public MessageResponse ToMessageResponse()
    {
        return new MessageResponse
        {
            Id = Id,
            Object = "thread.message",
            CreatedAt = CreatedAt,
            ThreadId = ThreadId,
            Role = Role,
            Content = string.IsNullOrEmpty(ContentJson)
                ? null
                : JsonSerializer.Deserialize<object>(ContentJson),
            FileIds = string.IsNullOrEmpty(FileIdsJson)
                ? new List<string>()
                : JsonSerializer.Deserialize<List<string>>(FileIdsJson) ?? new List<string>(),
            Metadata = string.IsNullOrEmpty(MetadataJson)
                ? new Dictionary<string, string>()
                : JsonSerializer.Deserialize<Dictionary<string, string>>(MetadataJson)
                ?? new Dictionary<string, string>(),
            AssistantId = AssistantId,
            RunId = RunId
        };
    }
}