namespace VanPattenAIRouter.Models;

/// <summary>
///     Represents a list of files response compatible with OpenAI API format.
/// </summary>
public class FileListResponse
{
    /// <summary>
    ///     The object type, which is always "list".
    /// </summary>
    public string Object { get; set; } = "list";

    /// <summary>
    ///     The list of files.
    /// </summary>
    public List<FileObject> Data { get; set; } = new();

    /// <summary>
    ///     The first file ID in the list (for pagination).
    /// </summary>
    public string? FirstId { get; set; }

    /// <summary>
    ///     The last file ID in the list (for pagination).
    /// </summary>
    public string? LastId { get; set; }

    /// <summary>
    ///     Whether there are more files available.
    /// </summary>
    public bool HasMore { get; set; }
}