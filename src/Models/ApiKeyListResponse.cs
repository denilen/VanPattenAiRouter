namespace VanPattenAIRouter.Models;

/// <summary>
///     Represents a list of API keys response.
/// </summary>
public class ApiKeyListResponse
{
    /// <summary>
    ///     The object type, which is always "list".
    /// </summary>
    public string Object { get; set; } = "list";

    /// <summary>
    ///     The list of API keys.
    /// </summary>
    public List<ApiKeyResponse> Data { get; set; } = new();

    /// <summary>
    ///     Whether there are more API keys.
    /// </summary>
    public bool HasMore { get; set; }
}