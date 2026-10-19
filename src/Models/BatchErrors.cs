namespace VanPattenAIRouter.Models;

/// <summary>
///     Represents batch errors.
/// </summary>
public class BatchErrors
{
    /// <summary>
    ///     The object type, which is always "list".
    /// </summary>
    public string Object { get; set; } = "list";

    /// <summary>
    ///     The list of errors.
    /// </summary>
    public List<BatchError> Data { get; set; } = new();
}