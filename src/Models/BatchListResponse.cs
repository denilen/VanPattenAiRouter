namespace VanPattenAIRouter.Models;

/// <summary>
///     Represents a list of batches response.
/// </summary>
public class BatchListResponse
{
    /// <summary>
    ///     The object type, which is always "list".
    /// </summary>
    public string Object { get; set; } = "list";

    /// <summary>
    ///     The list of batches.
    /// </summary>
    public List<BatchResponse> Data { get; set; } = new();

    /// <summary>
    ///     The first batch ID in the list (for pagination).
    /// </summary>
    public string? FirstId { get; set; }

    /// <summary>
    ///     The last batch ID in the list (for pagination).
    /// </summary>
    public string? LastId { get; set; }

    /// <summary>
    ///     Whether there are more batches available.
    /// </summary>
    public bool HasMore { get; set; }
}