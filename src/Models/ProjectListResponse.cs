namespace VanPattenAIRouter.Models;

/// <summary>
///     Represents a list of projects response.
/// </summary>
public class ProjectListResponse
{
    /// <summary>
    ///     The object type, which is always "list".
    /// </summary>
    public string Object { get; set; } = "list";

    /// <summary>
    ///     The list of projects.
    /// </summary>
    public List<ProjectResponse> Data { get; set; } = new();

    /// <summary>
    ///     Whether there are more projects.
    /// </summary>
    public bool HasMore { get; set; }
}