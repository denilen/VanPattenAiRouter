namespace VanPattenAIRouter.Models;

/// <summary>
///     Represents a text completion response compatible with OpenAI legacy API format.
/// </summary>
public class CompletionResponse
{
    /// <summary>
    ///     A unique identifier for the completion.
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    ///     The object type, which is always "text_completion".
    /// </summary>
    public string Object { get; set; } = "text_completion";

    /// <summary>
    ///     The Unix timestamp (in seconds) of when the completion was created.
    /// </summary>
    public long Created { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

    /// <summary>
    ///     The model used for the completion.
    /// </summary>
    public string Model { get; set; } = "";

    /// <summary>
    ///     The list of completion choices.
    /// </summary>
    public List<CompletionChoice> Choices { get; set; } = new();

    /// <summary>
    ///     Usage statistics for the completion request.
    /// </summary>
    public Usage Usage { get; set; } = new();

    /// <summary>
    ///     This fingerprint represents the backend configuration that the model runs with.
    /// </summary>
    public string? SystemFingerprint { get; set; }
}