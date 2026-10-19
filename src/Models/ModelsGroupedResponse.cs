namespace VanPattenAIRouter.Models;

/// <summary>
///     Represents a grouped models response for the /v1/models?grouped=true endpoint.
/// </summary>
public class ModelsGroupedResponse
{
    /// <summary>
    ///     The object type, which is always "list".
    /// </summary>
    public string Object { get; set; } = "list";

    /// <summary>
    ///     The list of model groups.
    /// </summary>
    public List<ModelGroup> Groups { get; set; } = new();
}