namespace VanPattenAIRouter.Models;

/// <summary>
///     Represents a fine-tuning error.
/// </summary>
public class FineTuningError
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
}