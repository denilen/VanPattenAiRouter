namespace VanPattenAIRouter.Models;

/// <summary>
///     Represents a group of models in the grouped models response.
/// </summary>
public class ModelGroup
{
    /// <summary>
    ///     The name of the model group.
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>
    ///     The type of the group. Can be "free" for free models group or "provider" for provider-specific group.
    /// </summary>
    public string Type { get; set; } = "";

    /// <summary>
    ///     The provider name associated with this group.
    /// </summary>
    public string Provider { get; set; } = "";

    /// <summary>
    ///     The list of models in this group.
    /// </summary>
    public List<ModelInfo> Models { get; set; } = new();
}