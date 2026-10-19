namespace VanPattenAIRouter.Models;

/// <summary>
///     Represents a list of invites response.
/// </summary>
public class InviteListResponse
{
    /// <summary>
    ///     The object type, which is always "list".
    /// </summary>
    public string Object { get; set; } = "list";

    /// <summary>
    ///     The list of invites.
    /// </summary>
    public List<InviteResponse> Data { get; set; } = new();

    /// <summary>
    ///     Whether there are more invites.
    /// </summary>
    public bool HasMore { get; set; }
}