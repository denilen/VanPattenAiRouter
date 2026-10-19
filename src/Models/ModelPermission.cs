using System.Text.Json.Serialization;

namespace VanPattenAIRouter.Models;

/// <summary>
///     Represents model permission.
/// </summary>
public class ModelPermission
{
    /// <summary>
    ///     The permission object type, always "model_permission".
    /// </summary>
    [JsonPropertyName("object")]
    public string Object { get; set; } = "model_permission";

    /// <summary>
    ///     The ID of the permission.
    /// </summary>
    public string Id { get; set; } = "";

    /// <summary>
    ///     The Unix timestamp when the permission was created.
    /// </summary>
    public long Created { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

    /// <summary>
    ///     Whether this permission allows creating completions.
    /// </summary>
    [JsonPropertyName("allow_completions")]
    public bool AllowCompletions { get; set; } = true;

    /// <summary>
    ///     Whether this permission allows creating chat completions.
    /// </summary>
    [JsonPropertyName("allow_chat_completions")]
    public bool AllowChatCompletions { get; set; } = true;

    /// <summary>
    ///     Whether this permission allows creating embeddings.
    /// </summary>
    [JsonPropertyName("allow_embeddings")]
    public bool AllowEmbeddings { get; set; } = true;

    /// <summary>
    ///     Whether this permission allows fine-tuning.
    /// </summary>
    [JsonPropertyName("allow_fine_tuning")]
    public bool AllowFineTuning { get; set; } = false;

    /// <summary>
    ///     Whether this permission allows sampling.
    /// </summary>
    [JsonPropertyName("allow_sampling")]
    public bool AllowSampling { get; set; } = true;

    /// <summary>
    ///     Whether this permission allows logprobs.
    /// </summary>
    [JsonPropertyName("allow_logprobs")]
    public bool AllowLogprobs { get; set; } = true;

    /// <summary>
    ///     Whether this permission allows search indices.
    /// </summary>
    [JsonPropertyName("allow_search_indices")]
    public bool AllowSearchIndices { get; set; } = false;

    /// <summary>
    ///     Whether this permission allows view.
    /// </summary>
    [JsonPropertyName("allow_view")]
    public bool AllowView { get; set; } = true;

    /// <summary>
    ///     The model this permission applies to.
    /// </summary>
    [JsonPropertyName("model")]
    public string Model { get; set; } = "";

    /// <summary>
    ///     The organization this permission belongs to.
    /// </summary>
    [JsonPropertyName("organization")]
    public string? Organization { get; set; }

    /// <summary>
    ///     The group this permission belongs to.
    /// </summary>
    [JsonPropertyName("group")]
    public string? Group { get; set; }

    /// <summary>
    ///     Whether this permission is blocking.
    /// </summary>
    [JsonPropertyName("is_blocking")]
    public bool IsBlocking { get; set; } = false;
}