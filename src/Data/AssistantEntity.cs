using System.Text.Json;
using VanPattenAIRouter.Models;

namespace VanPattenAIRouter.Data;

/// <summary>
///     Represents an assistant entity stored in the database.
/// </summary>
public class AssistantEntity
{
    /// <summary>
    ///     Unique identifier for the assistant (OpenAI format: asst_...).
    /// </summary>
    public string Id { get; set; } = "";

    /// <summary>
    ///     The name of the assistant.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    ///     The description of the assistant.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    ///     The model used by the assistant.
    /// </summary>
    public string Model { get; set; } = "";

    /// <summary>
    ///     The system instructions for the assistant.
    /// </summary>
    public string? Instructions { get; set; }

    /// <summary>
    ///     The tools available to the assistant (JSON).
    /// </summary>
    public string ToolsJson { get; set; } = "[]";

    /// <summary>
    ///     The file IDs for the assistant (JSON).
    /// </summary>
    public string FileIdsJson { get; set; } = "[]";

    /// <summary>
    ///     Set of 16 key-value pairs that can be attached to an object (JSON).
    /// </summary>
    public string MetadataJson { get; set; } = "{}";

    /// <summary>
    ///     The response format for the assistant (JSON).
    /// </summary>
    public string ResponseFormatJson { get; set; } = "{}";

    /// <summary>
    ///     The temperature for the assistant.
    /// </summary>
    public double? Temperature { get; set; }

    /// <summary>
    ///     The top_p for the assistant.
    /// </summary>
    public double? TopP { get; set; }

    /// <summary>
    ///     Unix timestamp (in seconds) when the assistant was created.
    /// </summary>
    public long CreatedAt { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

    /// <summary>
    ///     Converts to AssistantResponse for API response.
    /// </summary>
    public AssistantResponse ToAssistantResponse()
    {
        return new AssistantResponse
        {
            Id = Id,
            Object = "assistant",
            CreatedAt = CreatedAt,
            Name = Name,
            Description = Description,
            Model = Model,
            Instructions = Instructions,
            Tools = string.IsNullOrEmpty(ToolsJson)
                ? new List<AssistantTool>()
                : JsonSerializer.Deserialize<List<AssistantTool>>(ToolsJson)
                ?? new List<AssistantTool>(),
            FileIds = string.IsNullOrEmpty(FileIdsJson)
                ? new List<string>()
                : JsonSerializer.Deserialize<List<string>>(FileIdsJson) ?? new List<string>(),
            Metadata = string.IsNullOrEmpty(MetadataJson)
                ? new Dictionary<string, string>()
                : JsonSerializer.Deserialize<Dictionary<string, string>>(MetadataJson)
                ?? new Dictionary<string, string>(),
            ResponseFormat = string.IsNullOrEmpty(ResponseFormatJson)
                ? null
                : JsonSerializer.Deserialize<object>(ResponseFormatJson),
            Temperature = Temperature,
            TopP = TopP
        };
    }
}