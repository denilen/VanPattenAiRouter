using System.Text.Json;
using VanPattenAIRouter.Models;

namespace VanPattenAIRouter.Data;

/// <summary>
///     Represents a run step entity stored in the database.
/// </summary>
public class RunStepEntity
{
    /// <summary>
    ///     Unique identifier for the run step (OpenAI format: step_...).
    /// </summary>
    public string Id { get; set; } = "";

    /// <summary>
    ///     The run ID this step belongs to.
    /// </summary>
    public string RunId { get; set; } = "";

    /// <summary>
    ///     The thread ID this step belongs to.
    /// </summary>
    public string ThreadId { get; set; } = "";

    /// <summary>
    ///     The assistant ID used for this step.
    /// </summary>
    public string AssistantId { get; set; } = "";

    /// <summary>
    ///     The type of the step. Can be "message_creation" or "tool_calls".
    /// </summary>
    public string Type { get; set; } = "message_creation";

    /// <summary>
    ///     The status of the step.
    /// </summary>
    public string Status { get; set; } = "in_progress";

    /// <summary>
    ///     The step details (JSON).
    /// </summary>
    public string StepDetailsJson { get; set; } = "{}";

    /// <summary>
    ///     The last error for the step (JSON).
    /// </summary>
    public string LastErrorJson { get; set; } = "{}";

    /// <summary>
    ///     The usage statistics for the step (JSON).
    /// </summary>
    public string UsageJson { get; set; } = "{}";

    /// <summary>
    ///     Unix timestamp (in seconds) when the step started.
    /// </summary>
    public long? StartedAt { get; set; }

    /// <summary>
    ///     Unix timestamp (in seconds) when the step completed.
    /// </summary>
    public long? CompletedAt { get; set; }

    /// <summary>
    ///     Unix timestamp (in seconds) when the step failed.
    /// </summary>
    public long? FailedAt { get; set; }

    /// <summary>
    ///     Unix timestamp (in seconds) when the step was created.
    /// </summary>
    public long CreatedAt { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

    /// <summary>
    ///     Converts to RunStepResponse for API response.
    /// </summary>
    public RunStepResponse ToRunStepResponse()
    {
        return new RunStepResponse
        {
            Id = Id,
            Object = "thread.run.step",
            CreatedAt = CreatedAt,
            RunId = RunId,
            ThreadId = ThreadId,
            AssistantId = AssistantId,
            Type = Type,
            Status = Status,
            StepDetails = string.IsNullOrEmpty(StepDetailsJson)
                ? null
                : JsonSerializer.Deserialize<RunStepDetails>(StepDetailsJson),
            LastError = string.IsNullOrEmpty(LastErrorJson)
                ? null
                : JsonSerializer.Deserialize<RunError>(LastErrorJson),
            StartedAt = StartedAt,
            CompletedAt = CompletedAt,
            FailedAt = FailedAt,
            Usage = string.IsNullOrEmpty(UsageJson)
                ? null
                : JsonSerializer.Deserialize<Usage>(UsageJson)
        };
    }
}