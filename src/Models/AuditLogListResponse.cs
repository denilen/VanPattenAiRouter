namespace VanPattenAIRouter.Models;

/// <summary>
///     Represents a list of audit log entries response.
/// </summary>
public class AuditLogListResponse
{
    /// <summary>
    ///     The object type, which is always "list".
    /// </summary>
    public string Object { get; set; } = "list";

    /// <summary>
    ///     The list of audit log entries.
    /// </summary>
    public List<AuditLogEntry> Data { get; set; } = new();

    /// <summary>
    ///     Whether there are more entries.
    /// </summary>
    public bool HasMore { get; set; }
}