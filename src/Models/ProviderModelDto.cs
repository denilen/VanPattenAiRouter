namespace VanPattenAIRouter.Models;

/// <summary>
///     Data transfer object for a provider model.
/// </summary>
public class ProviderModelDto
{
    /// <summary>
    ///     Unique identifier for the provider model.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    ///     The model name as exposed by the router.
    /// </summary>
    public string ModelName { get; set; } = "";

    /// <summary>
    ///     The model name as known by the provider (may differ from ModelName).
    /// </summary>
    public string? ProviderModelName { get; set; }

    /// <summary>
    ///     Whether this model is currently enabled.
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    ///     Whether this model is free to use (does not consume credits).
    /// </summary>
    public bool IsFree { get; set; } = false;

    /// <summary>
    ///     The mode of the model. Can be "normal", "auto-free", or "auto-all".
    /// </summary>
    public string Mode { get; set; } = "normal";
}