using System.Text.Json.Serialization;

namespace VanPattenAIRouter.Models;

/// <summary>
///     Represents information about a model.
/// </summary>
public class ModelInfo
{
    /// <summary>
    ///     The model identifier, which can be referenced in the API endpoints.
    /// </summary>
    public string Id { get; set; } = "";

    /// <summary>
    ///     The object type, which is always "model".
    /// </summary>
    public string Object { get; set; } = "model";

    /// <summary>
    ///     The Unix timestamp (in seconds) when the model was created.
    /// </summary>
    public long Created { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

    /// <summary>
    ///     The organization that owns the model.
    /// </summary>
    [JsonPropertyName("owned_by")]
    public string OwnedBy { get; set; } = "";

    /// <summary>
    ///     Indicates whether the model is free to use (does not consume credits).
    /// </summary>
    [JsonIgnore]
    public bool IsFree { get; set; } = false;

    /// <summary>
    ///     The provider name that offers this model.
    /// </summary>
    [JsonIgnore]
    public string Provider { get; set; } = "";
}

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