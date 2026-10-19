namespace VanPattenAIRouter.Models;

/// <summary>
///     Represents a file upload request (multipart/form-data).
/// </summary>
public class FileUploadRequest
{
    /// <summary>
    ///     The file to upload.
    /// </summary>
    public IFormFile? File { get; set; }

    /// <summary>
    ///     The intended purpose of the file. Supported values are "fine-tune", "fine-tune-results", "assistants",
    ///     "assistants_output", "batch", "batch_output".
    /// </summary>
    public string Purpose { get; set; } = "fine-tune";
}