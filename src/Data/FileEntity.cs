using VanPattenAIRouter.Models;

namespace VanPattenAIRouter.Data;

/// <summary>
///     Represents a file entity stored in the database.
/// </summary>
public class FileEntity
{
    /// <summary>
    ///     Unique identifier for the file (OpenAI format: file_...).
    /// </summary>
    public string Id { get; set; } = "";

    /// <summary>
    ///     Original filename of the uploaded file.
    /// </summary>
    public string Filename { get; set; } = "";

    /// <summary>
    ///     Size of the file in bytes.
    /// </summary>
    public long Bytes { get; set; }

    /// <summary>
    ///     The intended purpose of the file.
    /// </summary>
    public string Purpose { get; set; } = "fine-tune";

    /// <summary>
    ///     The status of the file (processed, error).
    /// </summary>
    public string Status { get; set; } = "processed";

    /// <summary>
    ///     Details about the file status if there was an error.
    /// </summary>
    public string? StatusDetails { get; set; }

    /// <summary>
    ///     Local storage path for the file.
    /// </summary>
    public string? StoragePath { get; set; }

    /// <summary>
    ///     Unix timestamp (in seconds) when the file was created.
    /// </summary>
    public long CreatedAt { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

    /// <summary>
    ///     Converts to FileObject for API response.
    /// </summary>
    public FileObject ToFileObject()
    {
        return new FileObject
        {
            Id = Id,
            Object = "file",
            Bytes = Bytes,
            CreatedAt = CreatedAt,
            Filename = Filename,
            Purpose = Purpose,
            Status = Status,
            StatusDetails = StatusDetails
        };
    }
}