namespace VanPattenAIRouter.Models;

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