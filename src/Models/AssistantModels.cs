using System.Text.Json.Serialization;

namespace VanPattenAIRouter.Models;

/// <summary>
///     Represents an assistant request compatible with OpenAI API format.
/// </summary>
public class AssistantRequest
{
    /// <summary>
    ///     The model to use for the assistant.
    /// </summary>
    [JsonPropertyName("model")]
    public string Model { get; set; } = "";

    /// <summary>
    ///     The name of the assistant.
    /// </summary>
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    /// <summary>
    ///     The description of the assistant.
    /// </summary>
    [JsonPropertyName("description")]
    public string? Description { get; set; }

    /// <summary>
    ///     The system instructions for the assistant.
    /// </summary>
    [JsonPropertyName("instructions")]
    public string? Instructions { get; set; }

    /// <summary>
    ///     The tools available to the assistant.
    /// </summary>
    [JsonPropertyName("tools")]
    public List<AssistantTool>? Tools { get; set; }

    /// <summary>
    ///     The file IDs for the assistant (for code_interpreter, file_search).
    /// </summary>
    [JsonPropertyName("file_ids")]
    public List<string>? FileIds { get; set; }

    /// <summary>
    ///     Set of 16 key-value pairs that can be attached to an object.
    /// </summary>
    [JsonPropertyName("metadata")]
    public Dictionary<string, string>? Metadata { get; set; }

    /// <summary>
    ///     The response format for the assistant.
    /// </summary>
    [JsonPropertyName("response_format")]
    public object? ResponseFormat { get; set; }

    /// <summary>
    ///     The temperature for the assistant.
    /// </summary>
    [JsonPropertyName("temperature")]
    public double? Temperature { get; set; }

    /// <summary>
    ///     The top_p for the assistant.
    /// </summary>
    [JsonPropertyName("top_p")]
    public double? TopP { get; set; }
}

/// <summary>
///     Represents an assistant tool.
/// </summary>
public class AssistantTool
{
    /// <summary>
    ///     The type of the tool. Can be "code_interpreter", "file_search", or "function".
    /// </summary>
    [JsonPropertyName("type")]
    public string Type { get; set; } = "function";

    /// <summary>
    ///     The function definition (when type is "function").
    /// </summary>
    [JsonPropertyName("function")]
    public FunctionDefinition? Function { get; set; }
}

/// <summary>
///     Represents an assistant response compatible with OpenAI API format.
/// </summary>
public class AssistantResponse
{
    /// <summary>
    ///     The unique identifier for the assistant.
    /// </summary>
    public string Id { get; set; } = "";

    /// <summary>
    ///     The object type, which is always "assistant".
    /// </summary>
    public string Object { get; set; } = "assistant";

    /// <summary>
    ///     The Unix timestamp (in seconds) when the assistant was created.
    /// </summary>
    public long CreatedAt { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

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
    ///     The tools available to the assistant.
    /// </summary>
    public List<AssistantTool>? Tools { get; set; }

    /// <summary>
    ///     The file IDs for the assistant.
    /// </summary>
    [JsonPropertyName("file_ids")]
    public List<string>? FileIds { get; set; }

    /// <summary>
    ///     Set of 16 key-value pairs that can be attached to an object.
    /// </summary>
    public Dictionary<string, string>? Metadata { get; set; }

    /// <summary>
    ///     The response format for the assistant.
    /// </summary>
    [JsonPropertyName("response_format")]
    public object? ResponseFormat { get; set; }

    /// <summary>
    ///     The temperature for the assistant.
    /// </summary>
    [JsonPropertyName("temperature")]
    public double? Temperature { get; set; }

    /// <summary>
    ///     The top_p for the assistant.
    /// </summary>
    [JsonPropertyName("top_p")]
    public double? TopP { get; set; }
}

/// <summary>
///     Represents a list of assistants response.
/// </summary>
public class AssistantListResponse
{
    /// <summary>
    ///     The object type, which is always "list".
    /// </summary>
    public string Object { get; set; } = "list";

    /// <summary>
    ///     The list of assistants.
    /// </summary>
    public List<AssistantResponse> Data { get; set; } = new();

    /// <summary>
    ///     The first assistant ID in the list (for pagination).
    /// </summary>
    public string? FirstId { get; set; }

    /// <summary>
    ///     The last assistant ID in the list (for pagination).
    /// </summary>
    public string? LastId { get; set; }

    /// <summary>
    ///     Whether there are more assistants available.
    /// </summary>
    public bool HasMore { get; set; }
}

/// <summary>
///     Represents a thread request.
/// </summary>
public class ThreadRequest
{
    /// <summary>
    ///     A list of messages to start the thread with.
    /// </summary>
    [JsonPropertyName("messages")]
    public List<ThreadMessageRequest>? Messages { get; set; }

    /// <summary>
    ///     Set of 16 key-value pairs that can be attached to an object.
    /// </summary>
    [JsonPropertyName("metadata")]
    public Dictionary<string, string>? Metadata { get; set; }
}

/// <summary>
///     Represents a message in a thread request.
/// </summary>
public class ThreadMessageRequest
{
    /// <summary>
    ///     The role of the message (user or assistant).
    /// </summary>
    [JsonPropertyName("role")]
    public string Role { get; set; } = "user";

    /// <summary>
    ///     The content of the message.
    /// </summary>
    [JsonPropertyName("content")]
    public string Content { get; set; } = "";

    /// <summary>
    ///     The file IDs attached to the message.
    /// </summary>
    [JsonPropertyName("file_ids")]
    public List<string>? FileIds { get; set; }

    /// <summary>
    ///     Set of 16 key-value pairs that can be attached to an object.
    /// </summary>
    [JsonPropertyName("metadata")]
    public Dictionary<string, string>? Metadata { get; set; }
}

/// <summary>
///     Represents a thread response.
/// </summary>
public class ThreadResponse
{
    /// <summary>
    ///     The unique identifier for the thread.
    /// </summary>
    public string Id { get; set; } = "";

    /// <summary>
    ///     The object type, which is always "thread".
    /// </summary>
    public string Object { get; set; } = "thread";

    /// <summary>
    ///     The Unix timestamp (in seconds) when the thread was created.
    /// </summary>
    public long CreatedAt { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

    /// <summary>
    ///     Set of 16 key-value pairs that can be attached to an object.
    /// </summary>
    public Dictionary<string, string>? Metadata { get; set; }
}

/// <summary>
///     Represents a list of threads response.
/// </summary>
public class ThreadListResponse
{
    /// <summary>
    ///     The object type, which is always "list".
    /// </summary>
    public string Object { get; set; } = "list";

    /// <summary>
    ///     The list of threads.
    /// </summary>
    public List<ThreadResponse> Data { get; set; } = new();

    /// <summary>
    ///     The first thread ID in the list (for pagination).
    /// </summary>
    public string? FirstId { get; set; }

    /// <summary>
    ///     The last thread ID in the list (for pagination).
    /// </summary>
    public string? LastId { get; set; }

    /// <summary>
    ///     Whether there are more threads available.
    /// </summary>
    public bool HasMore { get; set; }
}

/// <summary>
///     Represents a message request in a thread.
/// </summary>
public class MessageRequest
{
    /// <summary>
    ///     The role of the message (user or assistant).
    /// </summary>
    [JsonPropertyName("role")]
    public string Role { get; set; } = "user";

    /// <summary>
    ///     The content of the message.
    /// </summary>
    [JsonPropertyName("content")]
    public object? Content { get; set; }

    /// <summary>
    ///     The file IDs attached to the message.
    /// </summary>
    [JsonPropertyName("file_ids")]
    public List<string>? FileIds { get; set; }

    /// <summary>
    ///     Set of 16 key-value pairs that can be attached to an object.
    /// </summary>
    [JsonPropertyName("metadata")]
    public Dictionary<string, string>? Metadata { get; set; }
}

/// <summary>
///     Represents a message response.
/// </summary>
public class MessageResponse
{
    /// <summary>
    ///     The unique identifier for the message.
    /// </summary>
    public string Id { get; set; } = "";

    /// <summary>
    ///     The object type, which is always "thread.message".
    /// </summary>
    public string Object { get; set; } = "thread.message";

    /// <summary>
    ///     The Unix timestamp (in seconds) when the message was created.
    /// </summary>
    public long CreatedAt { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

    /// <summary>
    ///     The thread ID this message belongs to.
    /// </summary>
    [JsonPropertyName("thread_id")]
    public string ThreadId { get; set; } = "";

    /// <summary>
    ///     The role of the message (user or assistant).
    /// </summary>
    public string Role { get; set; } = "user";

    /// <summary>
    ///     The content of the message.
    /// </summary>
    public object? Content { get; set; }

    /// <summary>
    ///     The file IDs attached to the message.
    /// </summary>
    [JsonPropertyName("file_ids")]
    public List<string>? FileIds { get; set; }

    /// <summary>
    ///     Set of 16 key-value pairs that can be attached to an object.
    /// </summary>
    public Dictionary<string, string>? Metadata { get; set; }

    /// <summary>
    ///     The assistant ID that created this message (if assistant message).
    /// </summary>
    [JsonPropertyName("assistant_id")]
    public string? AssistantId { get; set; }

    /// <summary>
    ///     The run ID that created this message (if assistant message).
    /// </summary>
    [JsonPropertyName("run_id")]
    public string? RunId { get; set; }
}

/// <summary>
///     Represents a list of messages response.
/// </summary>
public class MessageListResponse
{
    /// <summary>
    ///     The object type, which is always "list".
    /// </summary>
    public string Object { get; set; } = "list";

    /// <summary>
    ///     The list of messages.
    /// </summary>
    public List<MessageResponse> Data { get; set; } = new();

    /// <summary>
    ///     The first message ID in the list (for pagination).
    /// </summary>
    public string? FirstId { get; set; }

    /// <summary>
    ///     The last message ID in the list (for pagination).
    /// </summary>
    public string? LastId { get; set; }

    /// <summary>
    ///     Whether there are more messages available.
    /// </summary>
    public bool HasMore { get; set; }
}

/// <summary>
///     Represents a run request.
/// </summary>
public class RunRequest
{
    /// <summary>
    ///     The ID of the assistant to use for the run.
    /// </summary>
    [JsonPropertyName("assistant_id")]
    public string AssistantId { get; set; } = "";

    /// <summary>
    ///     The model to use for the run (overrides assistant's model).
    /// </summary>
    [JsonPropertyName("model")]
    public string? Model { get; set; }

    /// <summary>
    ///     The instructions to use for the run (overrides assistant's instructions).
    /// </summary>
    [JsonPropertyName("instructions")]
    public string? Instructions { get; set; }

    /// <summary>
    ///     Additional messages to add to the thread before running.
    /// </summary>
    [JsonPropertyName("additional_messages")]
    public List<ThreadMessageRequest>? AdditionalMessages { get; set; }

    /// <summary>
    ///     The tools to use for the run (overrides assistant's tools).
    /// </summary>
    [JsonPropertyName("tools")]
    public List<AssistantTool>? Tools { get; set; }

    /// <summary>
    ///     The file IDs for the run.
    /// </summary>
    [JsonPropertyName("file_ids")]
    public List<string>? FileIds { get; set; }

    /// <summary>
    ///     Set of 16 key-value pairs that can be attached to an object.
    /// </summary>
    [JsonPropertyName("metadata")]
    public Dictionary<string, string>? Metadata { get; set; }

    /// <summary>
    ///     The temperature for the run.
    /// </summary>
    [JsonPropertyName("temperature")]
    public double? Temperature { get; set; }

    /// <summary>
    ///     The top_p for the run.
    /// </summary>
    [JsonPropertyName("top_p")]
    public double? TopP { get; set; }

    /// <summary>
    ///     Whether to stream the response.
    /// </summary>
    [JsonPropertyName("stream")]
    public bool? Stream { get; set; }

    /// <summary>
    ///     The maximum number of prompt tokens.
    /// </summary>
    [JsonPropertyName("max_prompt_tokens")]
    public int? MaxPromptTokens { get; set; }

    /// <summary>
    ///     The maximum number of completion tokens.
    /// </summary>
    [JsonPropertyName("max_completion_tokens")]
    public int? MaxCompletionTokens { get; set; }

    /// <summary>
    ///     The truncation strategy for the run.
    /// </summary>
    [JsonPropertyName("truncation_strategy")]
    public TruncationStrategy? TruncationStrategy { get; set; }

    /// <summary>
    ///     The tool choice for the run.
    /// </summary>
    [JsonPropertyName("tool_choice")]
    public object? ToolChoice { get; set; }

    /// <summary>
    ///     The response format for the run.
    /// </summary>
    [JsonPropertyName("response_format")]
    public object? ResponseFormat { get; set; }
}

/// <summary>
///     Represents a truncation strategy.
/// </summary>
public class TruncationStrategy
{
    /// <summary>
    ///     The truncation type. Can be "auto" or "last_messages".
    /// </summary>
    [JsonPropertyName("type")]
    public string Type { get; set; } = "auto";

    /// <summary>
    ///     The number of last messages to keep (when type is "last_messages").
    /// </summary>
    [JsonPropertyName("last_messages")]
    public int? LastMessages { get; set; }
}

/// <summary>
///     Represents a submit tool outputs request.
/// </summary>
public class SubmitToolOutputsRequest
{
    /// <summary>
    ///     The tool outputs to submit.
    /// </summary>
    [JsonPropertyName("tool_outputs")]
    public List<ToolOutput> ToolOutputs { get; set; } = new();

    /// <summary>
    ///     Whether to stream the response.
    /// </summary>
    [JsonPropertyName("stream")]
    public bool? Stream { get; set; }
}

/// <summary>
///     Represents a tool output.
/// </summary>
public class ToolOutput
{
    /// <summary>
    ///     The tool call ID.
    /// </summary>
    [JsonPropertyName("tool_call_id")]
    public string ToolCallId { get; set; } = "";

    /// <summary>
    ///     The output of the tool call.
    /// </summary>
    [JsonPropertyName("output")]
    public string Output { get; set; } = "";
}

/// <summary>
///     Represents a run response.
/// </summary>
public class RunResponse
{
    /// <summary>
    ///     The unique identifier for the run.
    /// </summary>
    public string Id { get; set; } = "";

    /// <summary>
    ///     The object type, which is always "thread.run".
    /// </summary>
    public string Object { get; set; } = "thread.run";

    /// <summary>
    ///     The Unix timestamp (in seconds) when the run was created.
    /// </summary>
    public long CreatedAt { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

    /// <summary>
    ///     The thread ID this run belongs to.
    /// </summary>
    [JsonPropertyName("thread_id")]
    public string ThreadId { get; set; } = "";

    /// <summary>
    ///     The assistant ID used for this run.
    /// </summary>
    [JsonPropertyName("assistant_id")]
    public string AssistantId { get; set; } = "";

    /// <summary>
    ///     The current status of the run.
    /// </summary>
    public string Status { get; set; } = "queued";

    /// <summary>
    ///     The model used for the run.
    /// </summary>
    public string Model { get; set; } = "";

    /// <summary>
    ///     The instructions used for the run.
    /// </summary>
    public string? Instructions { get; set; }

    /// <summary>
    ///     The tools used for the run.
    /// </summary>
    public List<AssistantTool>? Tools { get; set; }

    /// <summary>
    ///     The file IDs for the run.
    /// </summary>
    [JsonPropertyName("file_ids")]
    public List<string>? FileIds { get; set; }

    /// <summary>
    ///     Set of 16 key-value pairs that can be attached to an object.
    /// </summary>
    public Dictionary<string, string>? Metadata { get; set; }

    /// <summary>
    ///     The temperature for the run.
    /// </summary>
    [JsonPropertyName("temperature")]
    public double? Temperature { get; set; }

    /// <summary>
    ///     The top_p for the run.
    /// </summary>
    [JsonPropertyName("top_p")]
    public double? TopP { get; set; }

    /// <summary>
    ///     The maximum number of prompt tokens.
    /// </summary>
    [JsonPropertyName("max_prompt_tokens")]
    public int? MaxPromptTokens { get; set; }

    /// <summary>
    ///     The maximum number of completion tokens.
    /// </summary>
    [JsonPropertyName("max_completion_tokens")]
    public int? MaxCompletionTokens { get; set; }

    /// <summary>
    ///     The truncation strategy for the run.
    /// </summary>
    [JsonPropertyName("truncation_strategy")]
    public TruncationStrategy? TruncationStrategy { get; set; }

    /// <summary>
    ///     The tool choice for the run.
    /// </summary>
    [JsonPropertyName("tool_choice")]
    public object? ToolChoice { get; set; }

    /// <summary>
    ///     The response format for the run.
    /// </summary>
    [JsonPropertyName("response_format")]
    public object? ResponseFormat { get; set; }

    /// <summary>
    ///     The Unix timestamp (in seconds) when the run started.
    /// </summary>
    [JsonPropertyName("started_at")]
    public long? StartedAt { get; set; }

    /// <summary>
    ///     The Unix timestamp (in seconds) when the run expired.
    /// </summary>
    [JsonPropertyName("expires_at")]
    public long? ExpiresAt { get; set; }

    /// <summary>
    ///     The Unix timestamp (in seconds) when the run completed.
    /// </summary>
    [JsonPropertyName("completed_at")]
    public long? CompletedAt { get; set; }

    /// <summary>
    ///     The required action for the run (when status is "requires_action").
    /// </summary>
    [JsonPropertyName("required_action")]
    public RequiredAction? RequiredAction { get; set; }

    /// <summary>
    ///     The last error for the run (when status is "failed").
    /// </summary>
    public RunError? LastError { get; set; }

    /// <summary>
    ///     The usage statistics for the run.
    /// </summary>
    public Usage? Usage { get; set; }

    /// <summary>
    ///     Whether the run is incomplete.
    /// </summary>
    [JsonPropertyName("incomplete_details")]
    public IncompleteDetails? IncompleteDetails { get; set; }
}

/// <summary>
///     Represents required action for a run.
/// </summary>
public class RequiredAction
{
    /// <summary>
    ///     The type of required action (always "submit_tool_outputs").
    /// </summary>
    [JsonPropertyName("type")]
    public string Type { get; set; } = "submit_tool_outputs";

    /// <summary>
    ///     The submit tool outputs action.
    /// </summary>
    [JsonPropertyName("submit_tool_outputs")]
    public SubmitToolOutputsAction? SubmitToolOutputs { get; set; }
}

/// <summary>
///     Represents submit tool outputs action.
/// </summary>
public class SubmitToolOutputsAction
{
    /// <summary>
    ///     The tool calls to submit outputs for.
    /// </summary>
    [JsonPropertyName("tool_calls")]
    public List<RunToolCall> ToolCalls { get; set; } = new();
}

/// <summary>
///     Represents a tool call in a run.
/// </summary>
public class RunToolCall
{
    /// <summary>
    ///     The unique identifier for the tool call.
    /// </summary>
    public string Id { get; set; } = "";

    /// <summary>
    ///     The type of the tool call (always "function").
    /// </summary>
    public string Type { get; set; } = "function";

    /// <summary>
    ///     The function call details.
    /// </summary>
    public Function Function { get; set; } = new();
}

/// <summary>
///     Represents a run error.
/// </summary>
public class RunError
{
    /// <summary>
    ///     The error code.
    /// </summary>
    public string Code { get; set; } = "";

    /// <summary>
    ///     The error message.
    /// </summary>
    public string Message { get; set; } = "";
}

/// <summary>
///     Represents incomplete details for a run.
/// </summary>
public class IncompleteDetails
{
    /// <summary>
    ///     The reason the run is incomplete.
    /// </summary>
    [JsonPropertyName("reason")]
    public string Reason { get; set; } = "";
}

/// <summary>
///     Represents a list of runs response.
/// </summary>
public class RunListResponse
{
    /// <summary>
    ///     The object type, which is always "list".
    /// </summary>
    public string Object { get; set; } = "list";

    /// <summary>
    ///     The list of runs.
    /// </summary>
    public List<RunResponse> Data { get; set; } = new();

    /// <summary>
    ///     The first run ID in the list (for pagination).
    /// </summary>
    public string? FirstId { get; set; }

    /// <summary>
    ///     The last run ID in the list (for pagination).
    /// </summary>
    public string? LastId { get; set; }

    /// <summary>
    ///     Whether there are more runs available.
    /// </summary>
    public bool HasMore { get; set; }
}

/// <summary>
///     Represents a run step response.
/// </summary>
public class RunStepResponse
{
    /// <summary>
    ///     The unique identifier for the run step.
    /// </summary>
    public string Id { get; set; } = "";

    /// <summary>
    ///     The object type, which is always "thread.run.step".
    /// </summary>
    public string Object { get; set; } = "thread.run.step";

    /// <summary>
    ///     The Unix timestamp (in seconds) when the run step was created.
    /// </summary>
    public long CreatedAt { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

    /// <summary>
    ///     The run ID this step belongs to.
    /// </summary>
    [JsonPropertyName("run_id")]
    public string RunId { get; set; } = "";

    /// <summary>
    ///     The thread ID this step belongs to.
    /// </summary>
    [JsonPropertyName("thread_id")]
    public string ThreadId { get; set; } = "";

    /// <summary>
    ///     The assistant ID used for this step.
    /// </summary>
    [JsonPropertyName("assistant_id")]
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
    ///     The step details.
    /// </summary>
    public RunStepDetails? StepDetails { get; set; }

    /// <summary>
    ///     The last error for the step (when status is "failed").
    /// </summary>
    public RunError? LastError { get; set; }

    /// <summary>
    ///     The Unix timestamp (in seconds) when the step started.
    /// </summary>
    [JsonPropertyName("started_at")]
    public long? StartedAt { get; set; }

    /// <summary>
    ///     The Unix timestamp (in seconds) when the step completed.
    /// </summary>
    [JsonPropertyName("completed_at")]
    public long? CompletedAt { get; set; }

    /// <summary>
    ///     The Unix timestamp (in seconds) when the step failed.
    /// </summary>
    [JsonPropertyName("failed_at")]
    public long? FailedAt { get; set; }

    /// <summary>
    ///     The usage statistics for the step.
    /// </summary>
    public Usage? Usage { get; set; }
}

/// <summary>
///     Represents run step details.
/// </summary>
public class RunStepDetails
{
    /// <summary>
    ///     The type of the step details.
    /// </summary>
    [JsonPropertyName("type")]
    public string Type { get; set; } = "message_creation";

    /// <summary>
    ///     The message creation details.
    /// </summary>
    [JsonPropertyName("message_creation")]
    public MessageCreationDetails? MessageCreation { get; set; }

    /// <summary>
    ///     The tool calls details.
    /// </summary>
    [JsonPropertyName("tool_calls")]
    public List<RunToolCall>? ToolCalls { get; set; }
}

/// <summary>
///     Represents message creation details.
/// </summary>
public class MessageCreationDetails
{
    /// <summary>
    ///     The message ID that was created.
    /// </summary>
    [JsonPropertyName("message_id")]
    public string MessageId { get; set; } = "";
}

/// <summary>
///     Represents a list of run steps response.
/// </summary>
public class RunStepListResponse
{
    /// <summary>
    ///     The object type, which is always "list".
    /// </summary>
    public string Object { get; set; } = "list";

    /// <summary>
    ///     The list of run steps.
    /// </summary>
    public List<RunStepResponse> Data { get; set; } = new();

    /// <summary>
    ///     The first run step ID in the list (for pagination).
    /// </summary>
    public string? FirstId { get; set; }

    /// <summary>
    ///     The last run step ID in the list (for pagination).
    /// </summary>
    public string? LastId { get; set; }

    /// <summary>
    ///     Whether there are more run steps available.
    /// </summary>
    public bool HasMore { get; set; }
}