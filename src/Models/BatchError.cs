namespace VanPattenAIRouter.Models;

/// <summary>
///     Represents a single batch error.
/// </summary>
public class BatchError
{
    /// <summary>
    ///     The error code.
    /// </summary>
    public string Code { get; set; } = "";

    /// <summary>
    ///     The error message.
    /// </summary>
    public string Message { get; set; } = "";

    /// <summary>
    ///     The parameter that caused the error.
    /// </summary>
    public string? Param { get; set; }

    /// <summary>
    ///     The line number in the input file where the error occurred.
    /// </summary>
    public int? Line { get; set; }
}