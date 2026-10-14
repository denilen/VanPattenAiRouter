using System.Text.Json.Serialization;

namespace VanPattenAIRouter.Models;

/// <summary>
///     Represents an embedding request compatible with OpenAI API format.
/// </summary>
public class EmbeddingRequest
{
    /// <summary>
    ///     ID of the model to use. You can use the "List models" API to see all of your available models.
    /// </summary>
    [JsonPropertyName("model")]
    public string Model { get; set; } = "";

    /// <summary>
    ///     Input text to embed, encoded as a string or array of tokens.
    /// </summary>
    [JsonPropertyName("input")]
    public List<string> Input { get; set; } = new();

    /// <summary>
    ///     The format to return the embeddings in. Can be "float" or "base64".
    /// </summary>
    [JsonPropertyName("encoding_format")]
    public string? EncodingFormat { get; set; }

    /// <summary>
    ///     The number of dimensions the resulting output embeddings should have.
    /// </summary>
    [JsonPropertyName("dimensions")]
    public int? Dimensions { get; set; }

    /// <summary>
    ///     A unique identifier representing your end-user.
    /// </summary>
    [JsonPropertyName("user")]
    public string? User { get; set; }
}

/// <summary>
///     Represents an embedding response compatible with OpenAI API format.
/// </summary>
public class EmbeddingResponse
{
    /// <summary>
    ///     The object type, which is always "list".
    /// </summary>
    public string Object { get; set; } = "list";

    /// <summary>
    ///     The list of embeddings.
    /// </summary>
    public List<EmbeddingData> Data { get; set; } = new();

    /// <summary>
    ///     The model used for the embedding.
    /// </summary>
    public string Model { get; set; } = "";

    /// <summary>
    ///     Usage statistics for the embedding request.
    /// </summary>
    public Usage Usage { get; set; } = new();
}

/// <summary>
///     Represents a single embedding data object.
/// </summary>
public class EmbeddingData
{
    /// <summary>
    ///     The object type, which is always "embedding".
    /// </summary>
    public string Object { get; set; } = "embedding";

    /// <summary>
    ///     The index of the embedding in the list of embeddings.
    /// </summary>
    public int Index { get; set; }

    /// <summary>
    ///     The embedding vector.
    /// </summary>
    public List<float> Embedding { get; set; } = new();
}