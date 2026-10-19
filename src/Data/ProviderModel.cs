namespace VanPattenAIRouter.Data;

/// <summary>
///     Represents a model offered by a provider.
/// </summary>
public class ProviderModel
{
    /// <summary>
    ///     Unique identifier for the provider model.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    ///     Foreign key to the provider.
    /// </summary>
    public int ProviderId { get; set; }

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
    public bool IsFree { get; set; }

    /// <summary>
    ///     The mode of the model. Can be "normal", "auto-free", or "auto-all".
    /// </summary>
    public string Mode { get; set; } = "normal";

    /// <summary>
    ///     Navigation property to the provider.
    /// </summary>
    public Provider Provider { get; set; } = null!;
}