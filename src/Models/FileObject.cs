namespace VanPattenAIRouter.Models;

/// <summary>
///     Represents a file object compatible with OpenAI API format.
/// </summary>
public class FileObject
{
    /// <summary>
    ///     The file identifier, which can be referenced in the API endpoints.
    /// </summary>
    public string Id { get; set; } = "";

    /// <summary>
    ///     The object type, which is always "file".
    /// </summary>
    public string Object { get; set; } = "file";

    /// <summary>
    ///     The size of the file in bytes.
    /// </summary>
    public long Bytes { get; set; }

    /// <summary>
    ///     The Unix timestamp (in seconds) when the file was created.
    /// </summary>
    public long CreatedAt { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

    /// <summary>
    ///     The name of the file.
    /// </summary>
    public string Filename { get; set; } = "";

    /// <summary>
    ///     The intended purpose of the file. Supported values are "fine-tune", "fine-tune-results", "assistants",
    ///     "assistants_output", "batch", "batch_output".
    /// </summary>
    public string Purpose { get; set; } = "fine-tune";

    /// <summary>
    ///     The status of the file. Supported values are "processed", "error".
    /// </summary>
    public string Status { get; set; } = "processed";

    /// <summary>
    ///     Details about the file status if there was an error.
    /// </summary>
    public string? StatusDetails { get; set; }
}