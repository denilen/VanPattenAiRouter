using System.Text.Json;
using VanPattenAIRouter.Models;

namespace VanPattenAIRouter.Data;

/// <summary>
///     Represents a run entity stored in the database.
/// </summary>
public class RunEntity
{
    /// <summary>
    ///     Unique identifier for the run (OpenAI format: run_...).
    /// </summary>
    public string Id { get; set; } = "";

    /// <summary>
    ///     The thread ID this run belongs to.
    /// </summary>
    public string ThreadId { get; set; } = "";

    /// <summary>
    ///     The assistant ID used for this run.
    /// </summary>
    public string AssistantId { get; set; } = "";

    /// <summary>
    ///     The model used for the run.
    /// </summary>
    public string Model { get; set; } = "";

    /// <summary>
    ///     The instructions used for the run.
    /// </summary>
    public string? Instructions { get; set; }

    /// <summary>
    ///     The tools used for the run (JSON).
    /// </summary>
    public string ToolsJson { get; set; } = "[]";

    /// <summary>
    ///     The file IDs for the run (JSON).
    /// </summary>
    public string FileIdsJson { get; set; } = "[]";

    /// <summary>
    ///     Set of 16 key-value pairs that can be attached to an object (JSON).
    /// </summary>
    public string MetadataJson { get; set; } = "{}";

    /// <summary>
    ///     The current status of the run.
    /// </summary>
    public string Status { get; set; } = "queued";

    /// <summary>
    ///     The required action for the run (JSON).
    /// </summary>
    public string RequiredActionJson { get; set; } = "{}";

    /// <summary>
    ///     The last error for the run (JSON).
    /// </summary>
    public string LastErrorJson { get; set; } = "{}";

    /// <summary>
    ///     The incomplete details for the run (JSON).
    /// </summary>
    public string IncompleteDetailsJson { get; set; } = "{}";

    /// <summary>
    ///     The response format for the run (JSON).
    /// </summary>
    public string ResponseFormatJson { get; set; } = "{}";

    /// <summary>
    ///     The tool choice for the run (JSON).
    /// </summary>
    public string ToolChoiceJson { get; set; } = "{}";

    /// <summary>
    ///     The truncation strategy for the run (JSON).
    /// </summary>
    public string TruncationStrategyJson { get; set; } = "{}";

    /// <summary>
    ///     Unix timestamp (in seconds) when the run started.
    /// </summary>
    public long? StartedAt { get; set; }

    /// <summary>
    ///     Unix timestamp (in seconds) when the run expired.
    /// </summary>
    public long? ExpiresAt { get; set; }

    /// <summary>
    ///     Unix timestamp (in seconds) when the run completed.
    /// </summary>
    public long? CompletedAt { get; set; }

    /// <summary>
    ///     Unix timestamp (in seconds) when the run failed.
    /// </summary>
    public long? FailedAt { get; set; }

    /// <summary>
    ///     The usage statistics for the run (JSON).
    /// </summary>
    public string UsageJson { get; set; } = "{}";

    /// <summary>
    ///     Unix timestamp (in seconds) when the run was created.
    /// </summary>
    public long CreatedAt { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

    /// <summary>
    ///     Converts to RunResponse for API response.
    /// </summary>
    public RunResponse ToRunResponse()
    {
        return new RunResponse
        {
            Id = Id,
            Object = "thread.run",
            CreatedAt = CreatedAt,
            ThreadId = ThreadId,
            AssistantId = AssistantId,
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
            Status = Status,
            Temperature = null, // Not stored in DB currently
            TopP = null,
            MaxPromptTokens = null,
            MaxCompletionTokens = null,
            TruncationStrategy = string.IsNullOrEmpty(TruncationStrategyJson)
                ? null
                : JsonSerializer.Deserialize<TruncationStrategy>(TruncationStrategyJson),
            ToolChoice = string.IsNullOrEmpty(ToolChoiceJson)
                ? null
                : JsonSerializer.Deserialize<object>(ToolChoiceJson),
            ResponseFormat = string.IsNullOrEmpty(ResponseFormatJson)
                ? null
                : JsonSerializer.Deserialize<object>(ResponseFormatJson),
            StartedAt = StartedAt,
            ExpiresAt = ExpiresAt,
            CompletedAt = CompletedAt,
            RequiredAction = string.IsNullOrEmpty(RequiredActionJson)
                ? null
                : JsonSerializer.Deserialize<RequiredAction>(RequiredActionJson),
            LastError = string.IsNullOrEmpty(LastErrorJson)
                ? null
                : JsonSerializer.Deserialize<RunError>(LastErrorJson),
            IncompleteDetails = string.IsNullOrEmpty(IncompleteDetailsJson)
                ? null
                : JsonSerializer.Deserialize<IncompleteDetails>(IncompleteDetailsJson),
            Usage = string.IsNullOrEmpty(UsageJson)
                ? null
                : JsonSerializer.Deserialize<Usage>(UsageJson)
        };
    }
}