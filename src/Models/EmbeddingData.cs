namespace VanPattenAIRouter.Models;

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