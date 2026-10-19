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
    ///     The model permissions.
    /// </summary>
    [JsonPropertyName("permission")]
    public List<ModelPermission>? Permission { get; set; }

    /// <summary>
    ///     The base model name (for fine-tuned models).
    /// </summary>
    [JsonPropertyName("root")]
    public string? Root { get; set; }

    /// <summary>
    ///     The parent model name (for fine-tuned models).
    /// </summary>
    [JsonPropertyName("parent")]
    public string? Parent { get; set; }

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