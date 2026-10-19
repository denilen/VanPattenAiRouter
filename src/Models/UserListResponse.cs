namespace VanPattenAIRouter.Models;

/// <summary>
///     Represents a list of users response.
/// </summary>
public class UserListResponse
{
    /// <summary>
    ///     The object type, which is always "list".
    /// </summary>
    public string Object { get; set; } = "list";

    /// <summary>
    ///     The list of users.
    /// </summary>
    public List<UserResponse> Data { get; set; } = new();

    /// <summary>
    ///     Whether there are more users.
    /// </summary>
    public bool HasMore { get; set; }
}