namespace VanPattenAIRouter.Models;

/// <summary>
///     Configuration for a model in a virtual provider.
/// </summary>
public class ProviderModelConfig
{
    /// <summary>
    ///     The model name as exposed by the router.
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>
    ///     Human-readable description of the model.
    /// </summary>
    public string Description { get; set; } = "";

    /// <summary>
    ///     The mode of the model. Can be "normal", "auto-free", or "auto-all".
    /// </summary>
    public string Mode { get; set; } = "normal";

    /// <summary>
    ///     Whether this model is enabled.
    /// </summary>
    public bool IsEnabled { get; set; } = true;
}