using System.Text.Json.Serialization;

namespace VanPattenAIRouter.Models;

/// <summary>
///     Represents an audit log entry.
/// </summary>
public class AuditLogEntry
{
    /// <summary>
    ///     The unique identifier for the audit log entry.
    /// </summary>
    public string Id { get; set; } = "";

    /// <summary>
    ///     The object type, which is always "audit_log".
    /// </summary>
    public string Object { get; set; } = "audit_log";

    /// <summary>
    ///     The type of action (api_key.created, project.updated, etc.).
    /// </summary>
    [JsonPropertyName("action")]
    public string Action { get; set; } = "";

    /// <summary>
    ///     The actor who performed the action.
    /// </summary>
    [JsonPropertyName("actor")]
    public AuditLogActor Actor { get; set; } = new();

    /// <summary>
    ///     The target of the action.
    /// </summary>
    [JsonPropertyName("target")]
    public AuditLogTarget Target { get; set; } = new();

    /// <summary>
    ///     The Unix timestamp (in seconds) when the action occurred.
    /// </summary>
    [JsonPropertyName("created_at")]
    public long CreatedAt { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

    /// <summary>
    ///     Additional context about the action.
    /// </summary>
    [JsonPropertyName("context")]
    public Dictionary<string, string>? Context { get; set; }
}