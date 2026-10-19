namespace VanPattenAIRouter.Models;

/// <summary>
///     Represents the target in an audit log entry.
/// </summary>
public class AuditLogTarget
{
    /// <summary>
    ///     The type of target (api_key, project, user, etc.).
    /// </summary>
    public string Type { get; set; } = "";

    /// <summary>
    ///     The ID of the target.
    /// </summary>
    public string Id { get; set; } = "";

    /// <summary>
    ///     The name of the target.
    /// </summary>
    public string Name { get; set; } = "";
}