namespace VanPattenAIRouter.Models;

/// <summary>
///     Represents the actor in an audit log entry.
/// </summary>
public class AuditLogActor
{
    /// <summary>
    ///     The type of actor (user, api_key, system).
    /// </summary>
    public string Type { get; set; } = "user";

    /// <summary>
    ///     The ID of the actor.
    /// </summary>
    public string Id { get; set; } = "";

    /// <summary>
    ///     The name of the actor.
    /// </summary>
    public string Name { get; set; } = "";
}