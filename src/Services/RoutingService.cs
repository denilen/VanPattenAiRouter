using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Serilog.Context;
using VanPattenAIRouter.Data;
using VanPattenAIRouter.Models;

namespace VanPattenAIRouter.Services;

/// <summary>
///     Exception thrown when a provider returns a non-retryable error (failover).
/// </summary>
public class ProviderFailoverException : Exception
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="ProviderFailoverException" /> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    public ProviderFailoverException(string message)
        : base(message)
    {
    }
}

/// <summary>
///     Interface for routing chat completions, embeddings, and streaming requests to providers.
/// </summary>
public interface IRoutingService
{
    /// <summary>
    ///     Routes a chat completion request to an appropriate provider.
    /// </summary>
    /// <param name="request">The chat completion request.</param>
    /// <param name="apiKey">The API key for the request (used for authentication).</param>
    /// <returns>The chat completion response.</returns>
    Task<ChatCompletionResponse> RouteChatCompletionAsync(ChatCompletionRequest request, string apiKey);

    /// <summary>
    ///     Routes a streaming chat completion request to an appropriate provider.
    /// </summary>
    /// <param name="request">The chat completion request with stream=true.</param>
    /// <param name="apiKey">The API key for the request.</param>
    /// <param name="outputStream">The stream to write SSE events to.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task RouteChatCompletionStreamAsync(ChatCompletionRequest request, string apiKey, Stream outputStream,
                                        CancellationToken cancellationToken);

    /// <summary>
    ///     Routes an embedding request to an appropriate provider.
    /// </summary>
    /// <param name="request">The embedding request.</param>
    /// <param name="apiKey">The API key for the request.</param>
    /// <returns>The embedding response.</returns>
    Task<EmbeddingResponse> RouteEmbeddingAsync(EmbeddingRequest request, string apiKey);

    /// <summary>
    ///     Routes a text completion request to an appropriate provider.
    /// </summary>
    /// <param name="request">The completion request.</param>
    /// <param name="apiKey">The API key for the request.</param>
    /// <returns>The completion response.</returns>
    Task<CompletionResponse> RouteCompletionAsync(CompletionRequest request, string apiKey);

    /// <summary>
    ///     Routes a streaming text completion request to an appropriate provider.
    /// </summary>
    /// <param name="request">The completion request with stream=true.</param>
    /// <param name="apiKey">The API key for the request.</param>
    /// <param name="outputStream">The stream to write SSE events to.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task RouteCompletionStreamAsync(CompletionRequest request, string apiKey, Stream outputStream,
                                    CancellationToken cancellationToken);

    /// <summary>
    ///     Routes a moderation request to an appropriate provider.
    /// </summary>
    /// <param name="request">The moderation request.</param>
    /// <param name="apiKey">The API key for the request.</param>
    /// <returns>The moderation response.</returns>
    Task<ModerationResponse> RouteModerationAsync(ModerationRequest request, string apiKey);

    /// <summary>
    ///     Routes an audio transcription request to an appropriate provider.
    /// </summary>
    /// <param name="request">The audio transcription request.</param>
    /// <param name="apiKey">The API key for the request.</param>
    /// <returns>The audio transcription response.</returns>
    Task<AudioTranscriptionResponse> RouteAudioTranscriptionAsync(AudioTranscriptionRequest request, string apiKey);

    /// <summary>
    ///     Routes an audio translation request to an appropriate provider.
    /// </summary>
    /// <param name="request">The audio translation request.</param>
    /// <param name="apiKey">The API key for the request.</param>
    /// <returns>The audio transcription response.</returns>
    Task<AudioTranscriptionResponse> RouteAudioTranslationAsync(AudioTranslationRequest request, string apiKey);

    /// <summary>
    ///     Routes an audio speech request to an appropriate provider.
    /// </summary>
    /// <param name="request">The audio speech request.</param>
    /// <param name="apiKey">The API key for the request.</param>
    /// <returns>The audio binary data.</returns>
    Task<byte[]> RouteAudioSpeechAsync(AudioSpeechRequest request, string apiKey);

    /// <summary>
    ///     Routes an image generation request to an appropriate provider.
    /// </summary>
    /// <param name="request">The image generation request.</param>
    /// <param name="apiKey">The API key for the request.</param>
    /// <returns>The image generation response.</returns>
    Task<ImageGenerationResponse> RouteImageGenerationAsync(ImageGenerationRequest request, string apiKey);

    /// <summary>
    ///     Routes an image edit request to an appropriate provider.
    /// </summary>
    /// <param name="request">The image edit request.</param>
    /// <param name="apiKey">The API key for the request.</param>
    /// <returns>The image generation response.</returns>
    Task<ImageGenerationResponse> RouteImageEditAsync(ImageEditRequest request, string apiKey);

    /// <summary>
    ///     Routes an image variation request to an appropriate provider.
    /// </summary>
    /// <param name="request">The image variation request.</param>
    /// <param name="apiKey">The API key for the request.</param>
    /// <returns>The image generation response.</returns>
    Task<ImageGenerationResponse> RouteImageVariationAsync(ImageVariationRequest request, string apiKey);

    /// <summary>
    ///     Routes a batch creation request.
    /// </summary>
    /// <param name="request">The batch request.</param>
    /// <param name="apiKey">The API key for the request.</param>
    /// <returns>The batch response.</returns>
    Task<BatchResponse> RouteBatchCreateAsync(BatchRequest request, string apiKey);

    /// <summary>
    ///     Retrieves a batch by ID.
    /// </summary>
    /// <param name="batchId">The batch ID.</param>
    /// <param name="apiKey">The API key for the request.</param>
    /// <returns>The batch response.</returns>
    Task<BatchResponse> RouteBatchRetrieveAsync(string batchId, string apiKey);

    /// <summary>
    ///     Lists batches with pagination.
    /// </summary>
    /// <param name="limit">Maximum number of batches to return.</param>
    /// <param name="after">Batch ID to start after (for pagination).</param>
    /// <param name="apiKey">The API key for the request.</param>
    /// <returns>The batch list response.</returns>
    Task<BatchListResponse> RouteBatchListAsync(int? limit, string? after, string apiKey);

    /// <summary>
    ///     Routes a batch cancellation request.
    /// </summary>
    /// <param name="batchId">The batch ID.</param>
    /// <param name="apiKey">The API key for the request.</param>
    /// <returns>The cancelled batch response.</returns>
    Task<BatchResponse> RouteBatchCancelAsync(string batchId, string apiKey);

    /// <summary>
    ///     Creates a fine-tuning job.
    /// </summary>
    /// <param name="request">The fine-tuning job request.</param>
    /// <param name="apiKey">The API key for the request.</param>
    /// <returns>The fine-tuning job response.</returns>
    Task<FineTuningJobResponse> RouteFineTuningCreateAsync(FineTuningJobRequest request, string apiKey);

    /// <summary>
    ///     Lists fine-tuning jobs.
    /// </summary>
    /// <param name="limit">Maximum number of jobs to return.</param>
    /// <param name="after">Job ID to start after (for pagination).</param>
    /// <param name="apiKey">The API key for the request.</param>
    /// <returns>The fine-tuning job list response.</returns>
    Task<FineTuningJobListResponse> RouteFineTuningListAsync(int? limit, string? after, string apiKey);

    /// <summary>
    ///     Retrieves a fine-tuning job by ID.
    /// </summary>
    /// <param name="jobId">The fine-tuning job ID.</param>
    /// <param name="apiKey">The API key for the request.</param>
    /// <returns>The fine-tuning job response.</returns>
    Task<FineTuningJobResponse> RouteFineTuningRetrieveAsync(string jobId, string apiKey);

    /// <summary>
    ///     Cancels a fine-tuning job.
    /// </summary>
    /// <param name="jobId">The fine-tuning job ID.</param>
    /// <param name="apiKey">The API key for the request.</param>
    /// <returns>The cancelled fine-tuning job response.</returns>
    Task<FineTuningJobResponse> RouteFineTuningCancelAsync(string jobId, string apiKey);

    /// <summary>
    ///     Lists fine-tuning job events.
    /// </summary>
    /// <param name="jobId">The fine-tuning job ID.</param>
    /// <param name="limit">Maximum number of events to return.</param>
    /// <param name="after">Event ID to start after (for pagination).</param>
    /// <param name="apiKey">The API key for the request.</param>
    /// <returns>The fine-tuning event list response.</returns>
    Task<FineTuningEventListResponse>
        RouteFineTuningEventsAsync(string jobId, int? limit, string? after, string apiKey);

    /// <summary>
    ///     Lists fine-tuning job checkpoints.
    /// </summary>
    /// <param name="jobId">The fine-tuning job ID.</param>
    /// <param name="limit">Maximum number of checkpoints to return.</param>
    /// <param name="after">Checkpoint ID to start after (for pagination).</param>
    /// <param name="apiKey">The API key for the request.</param>
    /// <returns>The fine-tuning checkpoint list response.</returns>
    Task<FineTuningCheckpointListResponse> RouteFineTuningCheckpointsAsync(
        string jobId, int? limit, string? after, string apiKey);

    /// <summary>
    ///     Uploads a file.
    /// </summary>
    /// <param name="request">The file upload request.</param>
    /// <param name="apiKey">The API key for the request.</param>
    /// <returns>The file object.</returns>
    Task<FileObject> RouteFileUploadAsync(FileUploadRequest request, string apiKey);

    /// <summary>
    ///     Lists files with pagination.
    /// </summary>
    /// <param name="purpose">Filter by purpose.</param>
    /// <param name="limit">Maximum number of files to return.</param>
    /// <param name="after">File ID to start after (for pagination).</param>
    /// <param name="apiKey">The API key for the request.</param>
    /// <returns>The file list response.</returns>
    Task<FileListResponse> RouteFileListAsync(string? purpose, int? limit, string? after, string apiKey);

    /// <summary>
    ///     Retrieves a file by ID.
    /// </summary>
    /// <param name="fileId">The file ID.</param>
    /// <param name="apiKey">The API key for the request.</param>
    /// <returns>The file object.</returns>
    Task<FileObject?> RouteFileRetrieveAsync(string fileId, string apiKey);

    /// <summary>
    ///     Deletes a file by ID.
    /// </summary>
    /// <param name="fileId">The file ID.</param>
    /// <param name="apiKey">The API key for the request.</param>
    /// <returns>True if deleted, false if not found.</returns>
    Task<bool> RouteFileDeleteAsync(string fileId, string apiKey);

    /// <summary>
    ///     Downloads file content by ID.
    /// </summary>
    /// <param name="fileId">The file ID.</param>
    /// <param name="apiKey">The API key for the request.</param>
    /// <returns>The file content as byte array, or null if not found.</returns>
    Task<byte[]?> RouteFileContentAsync(string fileId, string apiKey);

    /// <summary>
    ///     Creates an assistant.
    /// </summary>
    /// <param name="request">The assistant request.</param>
    /// <param name="apiKey">The API key for the request.</param>
    /// <returns>The assistant response.</returns>
    Task<AssistantResponse> RouteAssistantCreateAsync(AssistantRequest request, string apiKey);

    /// <summary>
    ///     Lists assistants with pagination.
    /// </summary>
    /// <param name="limit">Maximum number of assistants to return.</param>
    /// <param name="after">Assistant ID to start after (for pagination).</param>
    /// <param name="apiKey">The API key for the request.</param>
    /// <returns>The assistant list response.</returns>
    Task<AssistantListResponse> RouteAssistantListAsync(int? limit, string? after, string apiKey);

    /// <summary>
    ///     Retrieves an assistant by ID.
    /// </summary>
    /// <param name="assistantId">The assistant ID.</param>
    /// <param name="apiKey">The API key for the request.</param>
    /// <returns>The assistant response, or null if not found.</returns>
    Task<AssistantResponse?> RouteAssistantRetrieveAsync(string assistantId, string apiKey);

    /// <summary>
    ///     Updates an assistant.
    /// </summary>
    /// <param name="assistantId">The assistant ID.</param>
    /// <param name="request">The assistant update request.</param>
    /// <param name="apiKey">The API key for the request.</param>
    /// <returns>The updated assistant response, or null if not found.</returns>
    Task<AssistantResponse?> RouteAssistantUpdateAsync(string assistantId, AssistantRequest request, string apiKey);

    /// <summary>
    ///     Deletes an assistant by ID.
    /// </summary>
    /// <param name="assistantId">The assistant ID.</param>
    /// <param name="apiKey">The API key for the request.</param>
    /// <returns>True if deleted, false if not found.</returns>
    Task<bool> RouteAssistantDeleteAsync(string assistantId, string apiKey);

    /// <summary>
    ///     Creates a thread.
    /// </summary>
    /// <param name="request">The thread request.</param>
    /// <param name="apiKey">The API key for the request.</param>
    /// <returns>The thread response.</returns>
    Task<ThreadResponse> RouteThreadCreateAsync(ThreadRequest request, string apiKey);

    /// <summary>
    ///     Retrieves a thread by ID.
    /// </summary>
    /// <param name="threadId">The thread ID.</param>
    /// <param name="apiKey">The API key for the request.</param>
    /// <returns>The thread response, or null if not found.</returns>
    Task<ThreadResponse?> RouteThreadRetrieveAsync(string threadId, string apiKey);

    /// <summary>
    ///     Updates a thread.
    /// </summary>
    /// <param name="threadId">The thread ID.</param>
    /// <param name="request">The thread update request.</param>
    /// <param name="apiKey">The API key for the request.</param>
    /// <returns>The updated thread response, or null if not found.</returns>
    Task<ThreadResponse?> RouteThreadUpdateAsync(string threadId, ThreadRequest request, string apiKey);

    /// <summary>
    ///     Deletes a thread by ID.
    /// </summary>
    /// <param name="threadId">The thread ID.</param>
    /// <param name="apiKey">The API key for the request.</param>
    /// <returns>True if deleted, false if not found.</returns>
    Task<bool> RouteThreadDeleteAsync(string threadId, string apiKey);

    /// <summary>
    ///     Creates a message in a thread.
    /// </summary>
    /// <param name="threadId">The thread ID.</param>
    /// <param name="request">The message request.</param>
    /// <param name="apiKey">The API key for the request.</param>
    /// <returns>The message response.</returns>
    Task<MessageResponse> RouteMessageCreateAsync(string threadId, MessageRequest request, string apiKey);

    /// <summary>
    ///     Lists messages in a thread with pagination.
    /// </summary>
    /// <param name="threadId">The thread ID.</param>
    /// <param name="limit">Maximum number of messages to return.</param>
    /// <param name="after">Message ID to start after (for pagination).</param>
    /// <param name="before">Message ID to start before (for pagination).</param>
    /// <param name="apiKey">The API key for the request.</param>
    /// <returns>The message list response.</returns>
    Task<MessageListResponse> RouteMessageListAsync(string threadId, int? limit, string? after, string? before,
                                                    string apiKey);

    /// <summary>
    ///     Retrieves a message by ID.
    /// </summary>
    /// <param name="threadId">The thread ID.</param>
    /// <param name="messageId">The message ID.</param>
    /// <param name="apiKey">The API key for the request.</param>
    /// <returns>The message response, or null if not found.</returns>
    Task<MessageResponse?> RouteMessageRetrieveAsync(string threadId, string messageId, string apiKey);

    /// <summary>
    ///     Updates a message.
    /// </summary>
    /// <param name="threadId">The thread ID.</param>
    /// <param name="messageId">The message ID.</param>
    /// <param name="request">The message update request.</param>
    /// <param name="apiKey">The API key for the request.</param>
    /// <returns>The updated message response, or null if not found.</returns>
    Task<MessageResponse?> RouteMessageUpdateAsync(string threadId, string messageId, MessageRequest request,
                                                   string apiKey);

    /// <summary>
    ///     Creates a run in a thread.
    /// </summary>
    /// <param name="threadId">The thread ID.</param>
    /// <param name="request">The run request.</param>
    /// <param name="apiKey">The API key for the request.</param>
    /// <returns>The run response.</returns>
    Task<RunResponse> RouteRunCreateAsync(string threadId, RunRequest request, string apiKey);

    /// <summary>
    ///     Retrieves a run by ID.
    /// </summary>
    /// <param name="threadId">The thread ID.</param>
    /// <param name="runId">The run ID.</param>
    /// <param name="apiKey">The API key for the request.</param>
    /// <returns>The run response, or null if not found.</returns>
    Task<RunResponse?> RouteRunRetrieveAsync(string threadId, string runId, string apiKey);

    /// <summary>
    ///     Updates a run.
    /// </summary>
    /// <param name="threadId">The thread ID.</param>
    /// <param name="runId">The run ID.</param>
    /// <param name="request">The run update request.</param>
    /// <param name="apiKey">The API key for the request.</param>
    /// <returns>The updated run response, or null if not found.</returns>
    Task<RunResponse?> RouteRunUpdateAsync(string threadId, string runId, RunRequest request, string apiKey);

    /// <summary>
    ///     Cancels a run.
    /// </summary>
    /// <param name="threadId">The thread ID.</param>
    /// <param name="runId">The run ID.</param>
    /// <param name="apiKey">The API key for the request.</param>
    /// <returns>The cancelled run response, or null if not found.</returns>
    Task<RunResponse?> RouteRunCancelAsync(string threadId, string runId, string apiKey);

    /// <summary>
    ///     Submits tool outputs for a run.
    /// </summary>
    /// <param name="threadId">The thread ID.</param>
    /// <param name="runId">The run ID.</param>
    /// <param name="request">The submit tool outputs request.</param>
    /// <param name="apiKey">The API key for the request.</param>
    /// <returns>The updated run response, or null if not found.</returns>
    Task<RunResponse?> RouteRunSubmitToolOutputsAsync(string threadId, string runId, SubmitToolOutputsRequest request,
                                                      string apiKey);

    /// <summary>
    ///     Lists runs in a thread with pagination.
    /// </summary>
    /// <param name="threadId">The thread ID.</param>
    /// <param name="limit">Maximum number of runs to return.</param>
    /// <param name="after">Run ID to start after (for pagination).</param>
    /// <param name="apiKey">The API key for the request.</param>
    /// <returns>The run list response.</returns>
    Task<RunListResponse> RouteRunListAsync(string threadId, int? limit, string? after, string apiKey);

    /// <summary>
    ///     Retrieves a run step by ID.
    /// </summary>
    /// <param name="threadId">The thread ID.</param>
    /// <param name="runId">The run ID.</param>
    /// <param name="stepId">The run step ID.</param>
    /// <param name="apiKey">The API key for the request.</param>
    /// <returns>The run step response, or null if not found.</returns>
    Task<RunStepResponse?> RouteRunStepRetrieveAsync(string threadId, string runId, string stepId, string apiKey);

    /// <summary>
    ///     Lists run steps for a run with pagination.
    /// </summary>
    /// <param name="threadId">The thread ID.</param>
    /// <param name="runId">The run ID.</param>
    /// <param name="limit">Maximum number of steps to return.</param>
    /// <param name="after">Step ID to start after (for pagination).</param>
    /// <param name="apiKey">The API key for the request.</param>
    /// <returns>The run step list response.</returns>
    Task<RunStepListResponse> RouteRunStepListAsync(string threadId, string runId, int? limit, string? after,
                                                    string apiKey);

    // ==================== ADMIN/ORGANIZATION API ====================

    /// <summary>
    ///     Creates an API key.
    /// </summary>
    Task<ApiKeyResponse> RouteApiKeyCreateAsync(ApiKeyRequest request, string apiKey);

    /// <summary>
    ///     Lists API keys with pagination.
    /// </summary>
    Task<ApiKeyListResponse> RouteApiKeyListAsync(int? limit, string? after, string apiKey);

    /// <summary>
    ///     Retrieves an API key by ID.
    /// </summary>
    Task<ApiKeyResponse?> RouteApiKeyRetrieveAsync(string keyId, string apiKey);

    /// <summary>
    ///     Deletes an API key.
    /// </summary>
    Task<bool> RouteApiKeyDeleteAsync(string keyId, string apiKey);

    /// <summary>
    ///     Creates a project.
    /// </summary>
    Task<ProjectResponse> RouteProjectCreateAsync(ProjectRequest request, string apiKey);

    /// <summary>
    ///     Lists projects with pagination.
    /// </summary>
    Task<ProjectListResponse> RouteProjectListAsync(int? limit, string? after, string apiKey);

    /// <summary>
    ///     Retrieves a project by ID.
    /// </summary>
    Task<ProjectResponse?> RouteProjectRetrieveAsync(string projectId, string apiKey);

    /// <summary>
    ///     Updates a project.
    /// </summary>
    Task<ProjectResponse?> RouteProjectUpdateAsync(string projectId, ProjectRequest request, string apiKey);

    /// <summary>
    ///     Archives a project.
    /// </summary>
    Task<ProjectResponse?> RouteProjectArchiveAsync(string projectId, string apiKey);

    /// <summary>
    ///     Lists users in the organization.
    /// </summary>
    Task<UserListResponse> RouteUserListAsync(int? limit, string? after, string apiKey);

    /// <summary>
    ///     Retrieves the authenticated user.
    /// </summary>
    Task<UserResponse?> RouteUserRetrieveAsync(string userId, string apiKey);

    /// <summary>
    ///     Creates an invite.
    /// </summary>
    Task<InviteResponse> RouteInviteCreateAsync(InviteRequest request, string apiKey);

    /// <summary>
    ///     Lists invites with pagination.
    /// </summary>
    Task<InviteListResponse> RouteInviteListAsync(int? limit, string? after, string apiKey);

    /// <summary>
    ///     Deletes an invite.
    /// </summary>
    Task<bool> RouteInviteDeleteAsync(string inviteId, string apiKey);

    /// <summary>
    ///     Lists audit logs with pagination.
    /// </summary>
    Task<AuditLogListResponse> RouteAuditLogListAsync(int? limit, string? after, string apiKey);

    /// <summary>
    ///     Retrieves the organization.
    /// </summary>
    Task<OrganizationResponse?> RouteOrganizationRetrieveAsync(string apiKey);
}

/// <summary>
///     Service for routing AI requests to providers with failover, rate limiting, and virtual provider support.
/// </summary>
public class RoutingService : IRoutingService
{
    private readonly AiRouterDbContext _dbContext;
    private readonly string _fileStoragePath = "/tmp/vanpatten-files/";
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<RoutingService> _logger;
    private readonly IProviderService _providerService;
    private readonly RetryPolicyService _retryPolicyService;

    /// <summary>
    ///     Initializes a new instance of the <see cref="RoutingService" /> class.
    /// </summary>
    /// <param name="providerService">The provider service.</param>
    /// <param name="httpClientFactory">The HTTP client factory.</param>
    /// <param name="retryPolicyService">The retry policy service.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="dbContext">The database context.</param>
    public RoutingService(
        IProviderService providerService,
        IHttpClientFactory httpClientFactory,
        RetryPolicyService retryPolicyService,
        ILogger<RoutingService> logger,
        AiRouterDbContext dbContext)
    {
        _providerService = providerService;
        _httpClientFactory = httpClientFactory;
        _retryPolicyService = retryPolicyService;
        _logger = logger;
        _dbContext = dbContext;

        // Ensure file storage directory exists
        Directory.CreateDirectory(_fileStoragePath);
    }

    /// <inheritdoc />
    public async Task<ChatCompletionResponse> RouteChatCompletionAsync(ChatCompletionRequest request, string apiKey)
    {
        var stopwatch = Stopwatch.StartNew();

        // Estimate tokens from request (rough estimate: ~4 chars per token)
        var estimatedTokens = EstimateTokens(request.Messages);
        // DON'T add MaxTokens as it's output tokens, not input

        using (LogContext.PushProperty("Model", request.Model))
        using (LogContext.PushProperty("RequestId", Guid.NewGuid().ToString()[..8]))
        using (LogContext.PushProperty("EstimatedTokens", estimatedTokens))
        {
            _logger.LogDebug("Routing chat completion request for model: {Model} (estimated tokens: {Tokens})",
                             request.Model, estimatedTokens);

            // Resolve provider based on model name (handles virtual providers)
            var (provider, mode, specificModel)
                = await _providerService.ResolveAutoProviderAsync(request.Model, estimatedTokens);

            if (provider == null)
            {
                _logger.LogWarning(
                    "No available provider found for model: {Model} (all rate limited or not configured)",
                    request.Model);

                return new ChatCompletionResponse
                {
                    Model = request.Model,
                    Choices = new List<ChatChoice>
                    {
                        new()
                        {
                            Index = 0,
                            Message = new ChatMessage
                            {
                                Role = "assistant",
                                Content
                                    = $"No available provider for model '{request.Model}'. All providers are rate limited or not configured."
                            },
                            FinishReason = "rate_limit"
                        }
                    },
                    Usage = new Usage()
                };
            }

            // Check if it's a virtual provider (VanPatten/...)
            if (provider.IsVirtual)
                return await RouteVirtualProviderAsync(provider, request, apiKey, estimatedTokens, stopwatch, mode,
                                                       specificModel, default);

            return await RouteToSingleProviderAsync(provider, request, apiKey, estimatedTokens, stopwatch, default);
        }
    }

    /// <inheritdoc />
    public async Task RouteChatCompletionStreamAsync(ChatCompletionRequest request, string apiKey, Stream outputStream,
                                                     CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();

        // Estimate tokens from request - DON'T add MaxTokens as it's output tokens, not input
        var estimatedTokens = EstimateTokens(request.Messages);
        // estimatedTokens += request.MaxTokens ?? 1000; // REMOVED: MaxTokens is output, not input

        using (LogContext.PushProperty("Model", request.Model))
        using (LogContext.PushProperty("RequestId", Guid.NewGuid().ToString()[..8]))
        using (LogContext.PushProperty("EstimatedTokens", estimatedTokens))
        using (LogContext.PushProperty("Stream", true))
        {
            _logger.LogDebug(
                "Routing streaming chat completion request for model: {Model} (estimated tokens: {Tokens})",
                request.Model, estimatedTokens);

            try
            {
                // Resolve provider based on model name
                var (provider, mode, specificModel)
                    = await _providerService.ResolveAutoProviderAsync(request.Model, estimatedTokens);

                if (provider == null)
                {
                    _logger.LogWarning("No available provider found for model: {Model}", request.Model);
                    await WriteErrorEventAsync(outputStream, request.Model,
                                               "No available provider for model. All providers are rate limited or not configured.",
                                               "rate_limit", cancellationToken);
                    return;
                }

                // Check if it's a virtual provider
                if (provider.IsVirtual)
                {
                    await RouteVirtualProviderStreamAsync(provider, request, apiKey, estimatedTokens, outputStream,
                                                          stopwatch, mode, specificModel, cancellationToken);
                    return;
                }

                // Single provider streaming WITH FAILOVER
                await RouteSingleProviderStreamAsync(provider, request, apiKey, estimatedTokens, outputStream,
                                                     stopwatch, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // Client cancelled the request - this is expected, not an error
                _logger.LogInformation("Streaming request cancelled by client for model {Model} after {Elapsed}ms",
                                       request.Model, stopwatch.ElapsedMilliseconds);
                // Don't write error event - client already disconnected
            }
            catch (TaskCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // Some HTTP stacks throw TaskCanceledException instead of OperationCanceledException
                _logger.LogInformation("Streaming request cancelled by client for model {Model} after {Elapsed}ms",
                                       request.Model, stopwatch.ElapsedMilliseconds);
            }
            catch (ObjectDisposedException)
            {
                // Stream was disposed (client disconnected mid-stream) - not an error
                _logger.LogInformation(
                    "Streaming stream disposed for model {Model} after {Elapsed}ms (client likely disconnected)",
                    request.Model, stopwatch.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                // Never let exceptions bubble up during streaming - response headers already sent
                _logger.LogError("Unhandled error during streaming for model {Model}: {ErrorType}: {ErrorMessage}",
                                 request.Model, ex.GetType().Name, ex.Message);
                await WriteErrorEventAsync(outputStream, request.Model, $"Streaming error: {ex.Message}", "error",
                                           cancellationToken);
            }
        }
    }

    /// <inheritdoc />
    public async Task<EmbeddingResponse> RouteEmbeddingAsync(EmbeddingRequest request, string apiKey)
    {
        var stopwatch = Stopwatch.StartNew();

        // Estimate tokens for embeddings
        var estimatedTokens = EstimateTokens(request.Input);

        using (LogContext.PushProperty("Model", request.Model))
        using (LogContext.PushProperty("RequestId", Guid.NewGuid().ToString()[..8]))
        using (LogContext.PushProperty("EstimatedTokens", estimatedTokens))
        {
            _logger.LogDebug("Routing embedding request for model: {Model} (estimated tokens: {Tokens})", request.Model,
                             estimatedTokens);

            var (provider, mode, specificModel)
                = await _providerService.ResolveAutoProviderAsync(request.Model, estimatedTokens);

            if (provider == null)
            {
                _logger.LogWarning("No available provider found for embedding model: {Model}", request.Model);
                return new EmbeddingResponse
                {
                    Model = request.Model,
                    Data = new List<EmbeddingData>
                    {
                        new() { Object = "error", Index = 0, Embedding = new List<float>() }
                    },
                    Usage = new Usage()
                };
            }

            // Virtual provider not supported for embeddings yet - use first available provider
            if (provider.IsVirtual)
            {
                _logger.LogWarning(
                    "Virtual provider not supported for embeddings, falling back to first available embedding provider");
                var providers
                    = await _providerService.GetAvailableEmbeddingProvidersAsync(estimatedTokens);
                provider = providers.FirstOrDefault();

                if (provider == null)
                    return new EmbeddingResponse
                    {
                        Model = request.Model,
                        Data = new List<EmbeddingData>
                        {
                            new() { Object = "error", Index = 0, Embedding = new List<float>() }
                        },
                        Usage = new Usage()
                    };
            }

            try
            {
                return await RouteEmbeddingToProviderAsync(provider, request, apiKey, estimatedTokens, stopwatch,
                                                           default);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger.LogError(ex, "Provider {Provider} failed for embedding model {Model}", provider.Name,
                                 request.Model);
                return new EmbeddingResponse
                {
                    Model = request.Model,
                    Data = new List<EmbeddingData>
                    {
                        new() { Object = "error", Index = 0, Embedding = new List<float>() }
                    },
                    Usage = new Usage()
                };
            }
        }
    }

    /// <inheritdoc />
    public async Task<CompletionResponse> RouteCompletionAsync(CompletionRequest request, string apiKey)
    {
        var stopwatch = Stopwatch.StartNew();

        // Estimate tokens from prompt
        var estimatedTokens = EstimateTokensFromPrompt(request.Prompt);

        using (LogContext.PushProperty("Model", request.Model))
        using (LogContext.PushProperty("RequestId", Guid.NewGuid().ToString()[..8]))
        using (LogContext.PushProperty("EstimatedTokens", estimatedTokens))
        {
            _logger.LogDebug("Routing completion request for model: {Model} (estimated tokens: {Tokens})",
                             request.Model, estimatedTokens);

            try
            {
                var (provider, mode, specificModel)
                    = await _providerService.ResolveAutoProviderAsync(request.Model, estimatedTokens);

                if (provider == null)
                {
                    _logger.LogWarning("No available provider found for completion model: {Model}", request.Model);
                    return new CompletionResponse
                    {
                        Model = request.Model,
                        Choices = new List<CompletionChoice>
                        {
                            new()
                            {
                                Index = 0,
                                Text
                                    = "No available provider for model. All providers are rate limited or not configured.",
                                FinishReason = "error"
                            }
                        },
                        Usage = new Usage()
                    };
                }

                if (provider.IsVirtual)
                    return await RouteVirtualProviderAsync(provider, request, apiKey, estimatedTokens, stopwatch, mode,
                                                           specificModel, default);

                return await RouteCompletionToProviderAsync(provider, request, apiKey, estimatedTokens, stopwatch,
                                                            default);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger.LogError(ex, "Provider failed for completion model {Model}", request.Model);
                return new CompletionResponse
                {
                    Model = request.Model,
                    Choices = new List<CompletionChoice>
                    {
                        new() { Index = 0, Text = $"Error: {ex.Message}", FinishReason = "error" }
                    },
                    Usage = new Usage()
                };
            }
        }
    }

    /// <inheritdoc />
    public async Task RouteCompletionStreamAsync(CompletionRequest request, string apiKey, Stream outputStream,
                                                 CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();

        // Estimate tokens from prompt
        var estimatedTokens = EstimateTokensFromPrompt(request.Prompt);

        using (LogContext.PushProperty("Model", request.Model))
        using (LogContext.PushProperty("RequestId", Guid.NewGuid().ToString()[..8]))
        using (LogContext.PushProperty("EstimatedTokens", estimatedTokens))
        using (LogContext.PushProperty("Stream", true))
        {
            _logger.LogDebug("Routing streaming completion request for model: {Model} (estimated tokens: {Tokens})",
                             request.Model, estimatedTokens);

            try
            {
                var (provider, mode, specificModel)
                    = await _providerService.ResolveAutoProviderAsync(request.Model, estimatedTokens);

                if (provider == null)
                {
                    _logger.LogWarning("No available provider found for streaming completion model: {Model}",
                                       request.Model);
                    await WriteCompletionErrorEventAsync(outputStream, request.Model,
                                                         "No available provider for model. All providers are rate limited or not configured.",
                                                         "error", cancellationToken);
                    return;
                }

                if (provider.IsVirtual)
                {
                    await RouteVirtualProviderStreamAsync(provider, request, apiKey, estimatedTokens, outputStream,
                                                          stopwatch, mode, specificModel, cancellationToken);
                    return;
                }

                await RouteCompletionStreamToProviderAsync(provider, request, apiKey, estimatedTokens, outputStream,
                                                           cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                _logger.LogInformation("Streaming completion cancelled by client for model {Model}", request.Model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled error during streaming completion for model {Model}", request.Model);
                await WriteCompletionErrorEventAsync(outputStream, request.Model, $"Streaming error: {ex.Message}",
                                                     "error", cancellationToken);
            }
        }
    }

    /// <inheritdoc />
    public async Task<ModerationResponse> RouteModerationAsync(ModerationRequest request, string apiKey)
    {
        var stopwatch = Stopwatch.StartNew();

        var estimatedTokens = EstimateTokensFromModerationInput(request.Input);

        using (LogContext.PushProperty("Model", request.Model))
        using (LogContext.PushProperty("RequestId", Guid.NewGuid().ToString()[..8]))
        using (LogContext.PushProperty("EstimatedTokens", estimatedTokens))
        {
            _logger.LogDebug("Routing moderation request for model: {Model} (estimated tokens: {Tokens})",
                             request.Model, estimatedTokens);

            try
            {
                var (provider, mode, specificModel)
                    = await _providerService.ResolveAutoProviderAsync(request.Model, estimatedTokens);

                if (provider == null)
                {
                    _logger.LogWarning("No available provider found for moderation model: {Model}", request.Model);
                    return new ModerationResponse
                    {
                        Model = request.Model,
                        Results = new List<ModerationResult>
                        {
                            new()
                            {
                                Flagged = false,
                                Categories = new ModerationCategories(),
                                CategoryScores = new ModerationCategoryScores()
                            }
                        }
                    };
                }

                if (provider.IsVirtual)
                    return await RouteVirtualProviderModerationAsync(provider, request, apiKey, estimatedTokens,
                                                                     stopwatch, mode, specificModel, default);

                return await RouteModerationToProviderAsync(provider, request, apiKey, estimatedTokens, stopwatch,
                                                            default);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger.LogError(ex, "Provider failed for moderation model {Model}", request.Model);
                return new ModerationResponse
                {
                    Model = request.Model,
                    Results = new List<ModerationResult>
                    {
                        new()
                        {
                            Flagged = false,
                            Categories = new ModerationCategories(),
                            CategoryScores = new ModerationCategoryScores()
                        }
                    }
                };
            }
        }
    }

    /// <inheritdoc />
    public async Task<ImageGenerationResponse> RouteImageGenerationAsync(ImageGenerationRequest request, string apiKey)
    {
        var stopwatch = Stopwatch.StartNew();

        using (LogContext.PushProperty("Model", request.Model ?? "dall-e-3"))
        using (LogContext.PushProperty("RequestId", Guid.NewGuid().ToString()[..8]))
        {
            _logger.LogDebug("Routing image generation request for model: {Model}", request.Model ?? "dall-e-3");

            try
            {
                var (provider, mode, specificModel)
                    = await _providerService.ResolveAutoProviderAsync(request.Model ?? "dall-e-3");

                if (provider == null)
                {
                    _logger.LogWarning("No available provider found for image generation model: {Model}",
                                       request.Model);
                    return new ImageGenerationResponse
                    {
                        Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                        Data = new List<ImageData>
                        {
                            new()
                            {
                                RevisedPrompt
                                    = "No available provider for image generation. All providers are rate limited or not configured."
                            }
                        }
                    };
                }

                // Virtual provider not supported for images yet - use first available provider
                if (provider.IsVirtual)
                {
                    _logger.LogWarning(
                        "Virtual provider not supported for image generation, falling back to first available provider");
                    var providers
                        = await _providerService.GetAllAvailableProvidersForModelAsync(request.Model ?? "dall-e-3");
                    provider = providers.FirstOrDefault();

                    if (provider == null)
                        return new ImageGenerationResponse
                        {
                            Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                            Data = new List<ImageData>
                            {
                                new() { RevisedPrompt = "No available provider for image generation" }
                            }
                        };
                }

                return await RouteImageGenerationToProviderAsync(provider, request, apiKey, stopwatch, default);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger.LogError(ex, "Provider failed for image generation model {Model}", request.Model);
                return new ImageGenerationResponse
                {
                    Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                    Data = new List<ImageData>
                    {
                        new() { RevisedPrompt = $"Error: {ex.Message}" }
                    }
                };
            }
        }
    }

    /// <inheritdoc />
    public async Task<ImageGenerationResponse> RouteImageEditAsync(ImageEditRequest request, string apiKey)
    {
        var stopwatch = Stopwatch.StartNew();

        using (LogContext.PushProperty("Model", request.Model ?? "dall-e-2"))
        using (LogContext.PushProperty("RequestId", Guid.NewGuid().ToString()[..8]))
        {
            _logger.LogDebug("Routing image edit request for model: {Model}", request.Model ?? "dall-e-2");

            try
            {
                var (provider, mode, specificModel)
                    = await _providerService.ResolveAutoProviderAsync(request.Model ?? "dall-e-2");

                if (provider == null)
                {
                    _logger.LogWarning("No available provider found for image edit model: {Model}", request.Model);
                    return new ImageGenerationResponse
                    {
                        Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                        Data = new List<ImageData>
                        {
                            new()
                            {
                                RevisedPrompt
                                    = "No available provider for image edit. All providers are rate limited or not configured."
                            }
                        }
                    };
                }

                if (provider.IsVirtual)
                {
                    _logger.LogWarning(
                        "Virtual provider not supported for image edit, falling back to first available provider");
                    var providers
                        = await _providerService.GetAllAvailableProvidersForModelAsync(request.Model ?? "dall-e-2");
                    provider = providers.FirstOrDefault();

                    if (provider == null)
                        return new ImageGenerationResponse
                        {
                            Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                            Data = new List<ImageData>
                            {
                                new() { RevisedPrompt = "No available provider for image edit" }
                            }
                        };
                }

                return await RouteImageEditToProviderAsync(provider, request, apiKey, stopwatch, default);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger.LogError(ex, "Provider failed for image edit model {Model}", request.Model);
                return new ImageGenerationResponse
                {
                    Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                    Data = new List<ImageData>
                    {
                        new() { RevisedPrompt = $"Error: {ex.Message}" }
                    }
                };
            }
        }
    }

    /// <inheritdoc />
    public async Task<ImageGenerationResponse> RouteImageVariationAsync(ImageVariationRequest request, string apiKey)
    {
        var stopwatch = Stopwatch.StartNew();

        using (LogContext.PushProperty("Model", request.Model ?? "dall-e-2"))
        using (LogContext.PushProperty("RequestId", Guid.NewGuid().ToString()[..8]))
        {
            _logger.LogDebug("Routing image variation request for model: {Model}", request.Model ?? "dall-e-2");

            try
            {
                var (provider, mode, specificModel)
                    = await _providerService.ResolveAutoProviderAsync(request.Model ?? "dall-e-2");

                if (provider == null)
                {
                    _logger.LogWarning("No available provider found for image variation model: {Model}", request.Model);
                    return new ImageGenerationResponse
                    {
                        Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                        Data = new List<ImageData>
                        {
                            new()
                            {
                                RevisedPrompt
                                    = "No available provider for image variation. All providers are rate limited or not configured."
                            }
                        }
                    };
                }

                if (provider.IsVirtual)
                {
                    _logger.LogWarning(
                        "Virtual provider not supported for image variation, falling back to first available provider");
                    var providers
                        = await _providerService.GetAllAvailableProvidersForModelAsync(request.Model ?? "dall-e-2");
                    provider = providers.FirstOrDefault();

                    if (provider == null)
                        return new ImageGenerationResponse
                        {
                            Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                            Data = new List<ImageData>
                            {
                                new() { RevisedPrompt = "No available provider for image variation" }
                            }
                        };
                }

                return await RouteImageVariationToProviderAsync(provider, request, apiKey, stopwatch, default);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger.LogError(ex, "Provider failed for image variation model {Model}", request.Model);
                return new ImageGenerationResponse
                {
                    Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                    Data = new List<ImageData>
                    {
                        new() { RevisedPrompt = $"Error: {ex.Message}" }
                    }
                };
            }
        }
    }

    /// <inheritdoc />
    public async Task<AudioTranscriptionResponse> RouteAudioTranscriptionAsync(
        AudioTranscriptionRequest request, string apiKey)
    {
        var stopwatch = Stopwatch.StartNew();

        using (LogContext.PushProperty("Model", request.Model ?? "whisper-1"))
        using (LogContext.PushProperty("RequestId", Guid.NewGuid().ToString()[..8]))
        {
            _logger.LogDebug("Routing audio transcription request for model: {Model}", request.Model ?? "whisper-1");

            try
            {
                var (provider, mode, specificModel)
                    = await _providerService.ResolveAutoProviderAsync(request.Model ?? "whisper-1");

                if (provider == null)
                {
                    _logger.LogWarning("No available provider found for audio transcription model: {Model}",
                                       request.Model);
                    return new AudioTranscriptionResponse
                    {
                        Text
                            = "No available provider for audio transcription. All providers are rate limited or not configured."
                    };
                }

                if (provider.IsVirtual)
                {
                    _logger.LogWarning(
                        "Virtual provider not supported for audio transcription, falling back to first available provider");
                    var providers
                        = await _providerService.GetAllAvailableProvidersForModelAsync(request.Model ?? "whisper-1");
                    provider = providers.FirstOrDefault();

                    if (provider == null)
                        return new AudioTranscriptionResponse
                            { Text = "No available provider for audio transcription" };
                }

                return await RouteAudioTranscriptionToProviderAsync(provider, request, apiKey, stopwatch, default);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger.LogError(ex, "Provider failed for audio transcription model {Model}", request.Model);
                return new AudioTranscriptionResponse { Text = $"Error: {ex.Message}" };
            }
        }
    }

    /// <inheritdoc />
    public async Task<AudioTranscriptionResponse> RouteAudioTranslationAsync(
        AudioTranslationRequest request, string apiKey)
    {
        var stopwatch = Stopwatch.StartNew();

        using (LogContext.PushProperty("Model", request.Model ?? "whisper-1"))
        using (LogContext.PushProperty("RequestId", Guid.NewGuid().ToString()[..8]))
        {
            _logger.LogDebug("Routing audio translation request for model: {Model}", request.Model ?? "whisper-1");

            try
            {
                var (provider, mode, specificModel)
                    = await _providerService.ResolveAutoProviderAsync(request.Model ?? "whisper-1");

                if (provider == null)
                {
                    _logger.LogWarning("No available provider found for audio translation model: {Model}",
                                       request.Model);
                    return new AudioTranscriptionResponse
                    {
                        Text
                            = "No available provider for audio translation. All providers are rate limited or not configured."
                    };
                }

                if (provider.IsVirtual)
                {
                    _logger.LogWarning(
                        "Virtual provider not supported for audio translation, falling back to first available provider");
                    var providers
                        = await _providerService.GetAllAvailableProvidersForModelAsync(request.Model ?? "whisper-1");
                    provider = providers.FirstOrDefault();

                    if (provider == null)
                        return new AudioTranscriptionResponse { Text = "No available provider for audio translation" };
                }

                return await RouteAudioTranslationToProviderAsync(provider, request, apiKey, stopwatch, default);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger.LogError(ex, "Provider failed for audio translation model {Model}", request.Model);
                return new AudioTranscriptionResponse { Text = $"Error: {ex.Message}" };
            }
        }
    }

    /// <inheritdoc />
    public async Task<byte[]> RouteAudioSpeechAsync(AudioSpeechRequest request, string apiKey)
    {
        var stopwatch = Stopwatch.StartNew();

        using (LogContext.PushProperty("Model", request.Model ?? "tts-1"))
        using (LogContext.PushProperty("RequestId", Guid.NewGuid().ToString()[..8]))
        {
            _logger.LogDebug("Routing audio speech request for model: {Model}", request.Model ?? "tts-1");

            try
            {
                var (provider, mode, specificModel)
                    = await _providerService.ResolveAutoProviderAsync(request.Model ?? "tts-1");

                if (provider == null)
                {
                    _logger.LogWarning("No available provider found for audio speech model: {Model}", request.Model);
                    return Array.Empty<byte>();
                }

                if (provider.IsVirtual)
                {
                    _logger.LogWarning(
                        "Virtual provider not supported for audio speech, falling back to first available provider");
                    var providers
                        = await _providerService.GetAllAvailableProvidersForModelAsync(request.Model ?? "tts-1");
                    provider = providers.FirstOrDefault();

                    if (provider == null) return Array.Empty<byte>();
                }

                return await RouteAudioSpeechToProviderAsync(provider, request, apiKey, stopwatch, default);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger.LogError(ex, "Provider failed for audio speech model {Model}", request.Model);
                return Array.Empty<byte>();
            }
        }
    }

    /// <inheritdoc />
    public async Task<BatchResponse> RouteBatchCreateAsync(BatchRequest request, string apiKey)
    {
        var stopwatch = Stopwatch.StartNew();

        using (LogContext.PushProperty("RequestId", Guid.NewGuid().ToString()[..8]))
        {
            _logger.LogDebug("Routing batch creation request for endpoint: {Endpoint}", request.Endpoint);

            try
            {
                // For batch creation, we need a provider that supports the batch endpoint
                // Since this is a proxy, we'll create a batch record and proxy to the first available provider
                var (provider, mode, specificModel) = await _providerService.ResolveAutoProviderAsync("batch");

                if (provider == null)
                {
                    _logger.LogWarning("No available provider found for batch creation");
                    return new BatchResponse
                    {
                        Id = $"batch_{Guid.NewGuid().ToString("N")[..24]}",
                        Object = "batch",
                        Endpoint = request.Endpoint,
                        CompletionWindow = request.CompletionWindow,
                        Status = "failed",
                        InputFileId = request.InputFileId,
                        Errors = new BatchErrors
                        {
                            Object = "list",
                            Data = new List<BatchError>
                            {
                                new() { Code = "no_provider", Message = "No available provider for batch processing" }
                            }
                        },
                        CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                        ExpiresAt = DateTimeOffset.UtcNow.AddHours(24).ToUnixTimeSeconds(),
                        Metadata = request.Metadata
                    };
                }

                // For now, return a validating batch - actual processing would require file storage and async processing
                var batchId = $"batch_{Guid.NewGuid():N[..24]}";
                var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

                return new BatchResponse
                {
                    Id = batchId,
                    Object = "batch",
                    Endpoint = request.Endpoint,
                    CompletionWindow = request.CompletionWindow,
                    Status = "validating",
                    InputFileId = request.InputFileId,
                    CreatedAt = now,
                    ExpiresAt = now + 86400, // 24 hours
                    Metadata = request.Metadata,
                    Metrics = new BatchMetrics { TotalRequests = 0, CompletedRequests = 0, FailedRequests = 0 }
                };
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger.LogError(ex, "Provider failed for batch creation");
                return new BatchResponse
                {
                    Id = $"batch_{Guid.NewGuid():N[..24]}",
                    Object = "batch",
                    Endpoint = request.Endpoint,
                    CompletionWindow = request.CompletionWindow,
                    Status = "failed",
                    InputFileId = request.InputFileId,
                    Errors = new BatchErrors
                    {
                        Object = "list",
                        Data = new List<BatchError>
                        {
                            new() { Code = "server_error", Message = ex.Message }
                        }
                    },
                    CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                    Metadata = request.Metadata
                };
            }
        }
    }

    /// <inheritdoc />
    public async Task<BatchResponse> RouteBatchRetrieveAsync(string batchId, string apiKey)
    {
        using (LogContext.PushProperty("BatchId", batchId))
        {
            _logger.LogDebug("Retrieving batch: {BatchId}", batchId);

            // In a real implementation, this would fetch from a database
            // For now, return a mock response
            return new BatchResponse
            {
                Id = batchId,
                Object = "batch",
                Endpoint = "/v1/chat/completions",
                CompletionWindow = "24h",
                Status = "validating",
                InputFileId = $"file_{Guid.NewGuid().ToString("N")[..24]}",
                CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-5).ToUnixTimeSeconds(),
                ExpiresAt = DateTimeOffset.UtcNow.AddHours(24).ToUnixTimeSeconds(),
                Metrics = new BatchMetrics { TotalRequests = 0, CompletedRequests = 0, FailedRequests = 0 }
            };
        }
    }

    /// <inheritdoc />
    public async Task<BatchListResponse> RouteBatchListAsync(int? limit, string? after, string apiKey)
    {
        using (LogContext.PushProperty("Limit", limit))
        using (LogContext.PushProperty("After", after))
        {
            _logger.LogDebug("Listing batches with limit={Limit}, after={After}", limit, after);

            // In a real implementation, this would fetch from a database with pagination
            // For now, return an empty list
            return new BatchListResponse
            {
                Object = "list",
                Data = new List<BatchResponse>(),
                FirstId = null,
                LastId = null,
                HasMore = false
            };
        }
    }

    /// <inheritdoc />
    public async Task<BatchResponse> RouteBatchCancelAsync(string batchId, string apiKey)
    {
        using (LogContext.PushProperty("BatchId", batchId))
        {
            _logger.LogDebug("Cancelling batch: {BatchId}", batchId);

            // In a real implementation, this would update the batch status in the database
            // For now, return a mock cancelled response
            return new BatchResponse
            {
                Id = batchId,
                Object = "batch",
                Endpoint = "/v1/chat/completions",
                CompletionWindow = "24h",
                Status = "cancelled",
                InputFileId = $"file_{Guid.NewGuid().ToString("N")[..24]}",
                CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-10).ToUnixTimeSeconds(),
                CancelledAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                ExpiresAt = DateTimeOffset.UtcNow.AddHours(24).ToUnixTimeSeconds(),
                Metrics = new BatchMetrics { TotalRequests = 0, CompletedRequests = 0, FailedRequests = 0 }
            };
        }
    }

    /// <inheritdoc />
    public async Task<FineTuningJobResponse> RouteFineTuningCreateAsync(FineTuningJobRequest request, string apiKey)
    {
        using (LogContext.PushProperty("Model", request.Model))
        using (LogContext.PushProperty("TrainingFile", request.TrainingFile))
        {
            _logger.LogDebug("Creating fine-tuning job: model={Model}, training_file={TrainingFile}", request.Model,
                             request.TrainingFile);

            // Validate training file exists
            var trainingFile = await _dbContext.Files.FirstOrDefaultAsync(f => f.Id == request.TrainingFile);
            if (trainingFile == null) throw new ArgumentException($"Training file not found: {request.TrainingFile}");

            var (provider, mode, specificModel) = await _providerService.ResolveAutoProviderAsync(request.Model);

            if (provider == null)
            {
                _logger.LogWarning("No available provider found for fine-tuning model: {Model}", request.Model);
                return new FineTuningJobResponse
                {
                    Id = $"ftjob_{Guid.NewGuid().ToString("N")[..24]}",
                    Object = "fine_tuning.job",
                    Model = request.Model,
                    Status = "failed",
                    TrainingFile = request.TrainingFile,
                    ValidationFile = request.ValidationFile,
                    Hyperparameters = request.Hyperparameters,
                    Suffix = request.Suffix,
                    CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                    Error = new FineTuningError
                        { Code = "no_provider", Message = "No available provider for fine-tuning" }
                };
            }

            // For now, return a mock response - actual fine-tuning would proxy to provider
            var jobId = $"ftjob_{Guid.NewGuid().ToString("N")[..24]}";
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            return new FineTuningJobResponse
            {
                Id = jobId,
                Object = "fine_tuning.job",
                Model = request.Model,
                Status = "validating",
                TrainingFile = request.TrainingFile,
                ValidationFile = request.ValidationFile,
                Hyperparameters = request.Hyperparameters,
                Suffix = request.Suffix,
                CreatedAt = now,
                OrganizationId = null,
                ResultFiles = null,
                FinishedAt = null,
                FailedAt = null,
                Error = null,
                TrainedTokens = null
            };
        }
    }

    /// <inheritdoc />
    public async Task<FineTuningJobListResponse> RouteFineTuningListAsync(int? limit, string? after, string apiKey)
    {
        using (LogContext.PushProperty("Limit", limit))
        using (LogContext.PushProperty("After", after))
        {
            _logger.LogDebug("Listing fine-tuning jobs with limit={Limit}, after={After}", limit, after);

            // In a real implementation, this would fetch from a database
            // For now, return empty list
            return new FineTuningJobListResponse
            {
                Object = "list",
                Data = new List<FineTuningJobResponse>(),
                HasMore = false
            };
        }
    }

    /// <inheritdoc />
    public async Task<FineTuningJobResponse> RouteFineTuningRetrieveAsync(string jobId, string apiKey)
    {
        using (LogContext.PushProperty("JobId", jobId))
        {
            _logger.LogDebug("Retrieving fine-tuning job: {JobId}", jobId);

            // In a real implementation, this would fetch from a database
            // For now, return a mock response
            return new FineTuningJobResponse
            {
                Id = jobId,
                Object = "fine_tuning.job",
                Model = "gpt-3.5-turbo",
                Status = "succeeded",
                TrainingFile = "file_abc123",
                ValidationFile = null,
                Hyperparameters = new FineTuningHyperparameters
                    { NEpochs = 3, BatchSize = 4, LearningRateMultiplier = 1.0 },
                Suffix = "my-model",
                CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-10).ToUnixTimeSeconds(),
                OrganizationId = null,
                ResultFiles = new List<string> { "file_result123" },
                FinishedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                FailedAt = null,
                Error = null,
                TrainedTokens = 15000,
                FineTunedModel = "ft:gpt-3.5-turbo:my-org:my-model:abc123"
            };
        }
    }

    /// <inheritdoc />
    public async Task<FineTuningJobResponse> RouteFineTuningCancelAsync(string jobId, string apiKey)
    {
        using (LogContext.PushProperty("JobId", jobId))
        {
            _logger.LogDebug("Cancelling fine-tuning job: {JobId}", jobId);

            // In a real implementation, this would call the provider to cancel
            // For now, return a mock cancelled response
            return new FineTuningJobResponse
            {
                Id = jobId,
                Object = "fine_tuning.job",
                Model = "gpt-3.5-turbo",
                Status = "cancelled",
                TrainingFile = "file_abc123",
                ValidationFile = null,
                Hyperparameters = new FineTuningHyperparameters
                    { NEpochs = 3, BatchSize = 4, LearningRateMultiplier = 1.0 },
                Suffix = "my-model",
                CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-10).ToUnixTimeSeconds(),
                OrganizationId = null,
                ResultFiles = null,
                FinishedAt = null,
                FailedAt = null,
                Error = null,
                TrainedTokens = 0
            };
        }
    }

    /// <inheritdoc />
    public async Task<FineTuningEventListResponse> RouteFineTuningEventsAsync(
        string jobId, int? limit, string? after, string apiKey)
    {
        using (LogContext.PushProperty("JobId", jobId))
        using (LogContext.PushProperty("Limit", limit))
        using (LogContext.PushProperty("After", after))
        {
            _logger.LogDebug("Listing fine-tuning events for job: {JobId}", jobId);

            // In a real implementation, this would fetch from a database
            // For now, return mock events
            var events = new List<FineTuningEvent>
            {
                new()
                {
                    Id = $"ftevent_{Guid.NewGuid().ToString("N")[..24]}",
                    Object = "fine_tuning.job.event",
                    CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-5).ToUnixTimeSeconds(),
                    FineTuningJobId = jobId,
                    Message = "Job created",
                    Level = "info",
                    Type = "job_created"
                },
                new()
                {
                    Id = $"ftevent_{Guid.NewGuid().ToString("N")[..24]}",
                    Object = "fine_tuning.job.event",
                    CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-3).ToUnixTimeSeconds(),
                    FineTuningJobId = jobId,
                    Message = "Training started",
                    Level = "info",
                    Type = "training_started"
                }
            };

            return new FineTuningEventListResponse
            {
                Object = "list",
                Data = events,
                HasMore = false
            };
        }
    }

    /// <inheritdoc />
    public async Task<FineTuningCheckpointListResponse> RouteFineTuningCheckpointsAsync(
        string jobId, int? limit, string? after, string apiKey)
    {
        using (LogContext.PushProperty("JobId", jobId))
        using (LogContext.PushProperty("Limit", limit))
        using (LogContext.PushProperty("After", after))
        {
            _logger.LogDebug("Listing fine-tuning checkpoints for job: {JobId}", jobId);

            // In a real implementation, this would fetch from a database
            // For now, return mock checkpoints
            var checkpoints = new List<FineTuningCheckpoint>
            {
                new()
                {
                    Id = $"ftckpt_{Guid.NewGuid().ToString("N")[..24]}",
                    Object = "fine_tuning.checkpoint",
                    FineTuningJobId = jobId,
                    Step = 100,
                    Metrics = new FineTuningCheckpointMetrics
                    {
                        TrainingLoss = 1.23,
                        ValidationLoss = 1.45,
                        TrainingAccuracy = 0.85,
                        ValidationAccuracy = 0.82
                    },
                    FineTunedModel = null,
                    CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-5).ToUnixTimeSeconds()
                }
            };

            return new FineTuningCheckpointListResponse
            {
                Object = "list",
                Data = checkpoints,
                HasMore = false
            };
        }
    }

    /// <inheritdoc />
    public async Task<FileObject> RouteFileUploadAsync(FileUploadRequest request, string apiKey)
    {
        using (LogContext.PushProperty("Filename", request.File?.FileName ?? "unknown"))
        using (LogContext.PushProperty("Purpose", request.Purpose))
        {
            _logger.LogDebug("Uploading file: {Filename} with purpose: {Purpose}", request.File?.FileName,
                             request.Purpose);

            if (request.File == null || request.File.Length == 0) throw new ArgumentException("No file provided");

            // Validate purpose
            var validPurposes = new[]
                { "fine-tune", "fine-tune-results", "assistants", "assistants_output", "batch", "batch_output" };
            if (!validPurposes.Contains(request.Purpose))
                throw new ArgumentException(
                    $"Invalid purpose: {request.Purpose}. Must be one of: {string.Join(", ", validPurposes)}");

            // Generate file ID
            var fileId = $"file_{Guid.NewGuid().ToString("N")[..24]}";
            var fileName = request.File.FileName ?? "unknown";
            var fileSize = request.File.Length;
            var createdAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            // Create storage path
            var storageFileName = $"{fileId}_{fileName}";
            var storagePath = Path.Combine(_fileStoragePath, storageFileName);

            // Save file to local storage
            using (var fileStream = new FileStream(storagePath, FileMode.Create))
            {
                await request.File.CopyToAsync(fileStream);
            }

            // Create file entity
            var fileEntity = new FileEntity
            {
                Id = fileId,
                Filename = fileName,
                Bytes = fileSize,
                Purpose = request.Purpose,
                Status = "processed",
                StoragePath = storagePath,
                CreatedAt = createdAt
            };

            // Save to database
            _dbContext.Files.Add(fileEntity);
            await _dbContext.SaveChangesAsync();

            _logger.LogInformation("File uploaded successfully: {FileId} ({Filename}, {Size} bytes)", fileId, fileName,
                                   fileSize);

            return fileEntity.ToFileObject();
        }
    }

    /// <inheritdoc />
    public async Task<FileListResponse> RouteFileListAsync(string? purpose, int? limit, string? after, string apiKey)
    {
        using (LogContext.PushProperty("Purpose", purpose ?? "all"))
        using (LogContext.PushProperty("Limit", limit))
        using (LogContext.PushProperty("After", after))
        {
            _logger.LogDebug("Listing files with purpose={Purpose}, limit={Limit}, after={After}", purpose, limit,
                             after);

            var query = _dbContext.Files.AsQueryable();

            // Filter by purpose
            if (!string.IsNullOrEmpty(purpose)) query = query.Where(f => f.Purpose == purpose);

            // Filter by after (pagination cursor)
            if (!string.IsNullOrEmpty(after))
                query = query.Where(f => string.Compare(f.Id, after, StringComparison.Ordinal) > 0);

            // Order by ID (which includes timestamp prefix) for consistent pagination
            query = query.OrderBy(f => f.Id);

            // Apply limit (default 10000, max 10000 per OpenAI spec)
            var maxLimit = Math.Min(limit ?? 10000, 10000);
            var files = await query.Take(maxLimit + 1).ToListAsync(); // Take one extra to check hasMore

            var hasMore = files.Count > maxLimit;
            if (hasMore) files.RemoveAt(files.Count - 1);

            var fileObjects = files.Select(f => f.ToFileObject()).ToList();

            return new FileListResponse
            {
                Object = "list",
                Data = fileObjects,
                FirstId = fileObjects.FirstOrDefault()?.Id,
                LastId = fileObjects.LastOrDefault()?.Id,
                HasMore = hasMore
            };
        }
    }

    /// <inheritdoc />
    public async Task<FileObject?> RouteFileRetrieveAsync(string fileId, string apiKey)
    {
        using (LogContext.PushProperty("FileId", fileId))
        {
            _logger.LogDebug("Retrieving file: {FileId}", fileId);

            var fileEntity = await _dbContext.Files.FindAsync(fileId);
            if (fileEntity == null) return null;

            return fileEntity.ToFileObject();
        }
    }

    /// <inheritdoc />
    public async Task<bool> RouteFileDeleteAsync(string fileId, string apiKey)
    {
        using (LogContext.PushProperty("FileId", fileId))
        {
            _logger.LogDebug("Deleting file: {FileId}", fileId);

            var fileEntity = await _dbContext.Files.FindAsync(fileId);
            if (fileEntity == null) return false;

            // Delete physical file
            if (!string.IsNullOrEmpty(fileEntity.StoragePath) && File.Exists(fileEntity.StoragePath))
                try
                {
                    File.Delete(fileEntity.StoragePath);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to delete physical file: {StoragePath}", fileEntity.StoragePath);
                }

            // Delete from database
            _dbContext.Files.Remove(fileEntity);
            await _dbContext.SaveChangesAsync();

            _logger.LogInformation("File deleted successfully: {FileId}", fileId);
            return true;
        }
    }

    /// <inheritdoc />
    public async Task<byte[]?> RouteFileContentAsync(string fileId, string apiKey)
    {
        using (LogContext.PushProperty("FileId", fileId))
        {
            _logger.LogDebug("Downloading file content: {FileId}", fileId);

            var fileEntity = await _dbContext.Files.FindAsync(fileId);
            if (fileEntity == null || string.IsNullOrEmpty(fileEntity.StoragePath)
                || !File.Exists(fileEntity.StoragePath))
                return null;

            return await File.ReadAllBytesAsync(fileEntity.StoragePath);
        }
    }

    // ==================== ASSISTANTS API IMPLEMENTATION ====================

    /// <inheritdoc />
    public async Task<AssistantResponse> RouteAssistantCreateAsync(AssistantRequest request, string apiKey)
    {
        using (LogContext.PushProperty("Model", request.Model))
        using (LogContext.PushProperty("Name", request.Name ?? "unnamed"))
        {
            _logger.LogDebug("Creating assistant: model={Model}, name={Name}", request.Model,
                             request.Name ?? "unnamed");

            var assistantId = $"asst_{Guid.NewGuid().ToString("N")[..24]}";
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            var assistant = new AssistantEntity
            {
                Id = assistantId,
                Name = request.Name,
                Description = request.Description,
                Model = request.Model,
                Instructions = request.Instructions,
                ToolsJson = JsonSerializer.Serialize(request.Tools ?? new List<AssistantTool>()),
                FileIdsJson = JsonSerializer.Serialize(request.FileIds ?? new List<string>()),
                MetadataJson = JsonSerializer.Serialize(request.Metadata ?? new Dictionary<string, string>()),
                ResponseFormatJson = JsonSerializer.Serialize(request.ResponseFormat ?? new { }),
                Temperature = request.Temperature,
                TopP = request.TopP,
                CreatedAt = now
            };

            _dbContext.Assistants.Add(assistant);
            await _dbContext.SaveChangesAsync();

            _logger.LogInformation("Assistant created successfully: {AssistantId}", assistantId);
            return assistant.ToAssistantResponse();
        }
    }

    /// <inheritdoc />
    public async Task<AssistantListResponse> RouteAssistantListAsync(int? limit, string? after, string apiKey)
    {
        using (LogContext.PushProperty("Limit", limit))
        using (LogContext.PushProperty("After", after))
        {
            _logger.LogDebug("Listing assistants with limit={Limit}, after={After}", limit, after);

            var query = _dbContext.Assistants.OrderByDescending(a => a.CreatedAt).AsQueryable();

            if (!string.IsNullOrEmpty(after))
            {
                var afterTime = _dbContext.Assistants.Where(a => a.Id == after).Select(a => a.CreatedAt)
                                          .FirstOrDefault();
                if (afterTime > 0) query = query.Where(a => a.CreatedAt < afterTime);
            }

            var take = limit ?? 20;
            var assistants = await query.Take(take + 1).ToListAsync();

            var hasMore = assistants.Count > take;
            var data = hasMore ? assistants.Take(take).ToList() : assistants;

            return new AssistantListResponse
            {
                Object = "list",
                Data = data.Select(a => a.ToAssistantResponse()).ToList(),
                FirstId = data.FirstOrDefault()?.Id,
                LastId = data.LastOrDefault()?.Id,
                HasMore = hasMore
            };
        }
    }

    /// <inheritdoc />
    public async Task<AssistantResponse?> RouteAssistantRetrieveAsync(string assistantId, string apiKey)
    {
        using (LogContext.PushProperty("AssistantId", assistantId))
        {
            _logger.LogDebug("Retrieving assistant: {AssistantId}", assistantId);

            var assistant = await _dbContext.Assistants.FindAsync(assistantId);
            return assistant?.ToAssistantResponse();
        }
    }

    /// <inheritdoc />
    public async Task<AssistantResponse?> RouteAssistantUpdateAsync(string assistantId, AssistantRequest request,
                                                                    string apiKey)
    {
        using (LogContext.PushProperty("AssistantId", assistantId))
        {
            _logger.LogDebug("Updating assistant: {AssistantId}", assistantId);

            var assistant = await _dbContext.Assistants.FindAsync(assistantId);
            if (assistant == null) return null;

            if (!string.IsNullOrEmpty(request.Name)) assistant.Name = request.Name;
            if (!string.IsNullOrEmpty(request.Description)) assistant.Description = request.Description;
            if (!string.IsNullOrEmpty(request.Instructions)) assistant.Instructions = request.Instructions;
            if (request.Tools != null) assistant.ToolsJson = JsonSerializer.Serialize(request.Tools);
            if (request.FileIds != null) assistant.FileIdsJson = JsonSerializer.Serialize(request.FileIds);
            if (request.Metadata != null) assistant.MetadataJson = JsonSerializer.Serialize(request.Metadata);
            if (request.ResponseFormat != null)
                assistant.ResponseFormatJson = JsonSerializer.Serialize(request.ResponseFormat);
            if (request.Temperature.HasValue) assistant.Temperature = request.Temperature.Value;
            if (request.TopP.HasValue) assistant.TopP = request.TopP.Value;

            await _dbContext.SaveChangesAsync();

            _logger.LogInformation("Assistant updated successfully: {AssistantId}", assistantId);
            return assistant.ToAssistantResponse();
        }
    }

    /// <inheritdoc />
    public async Task<bool> RouteAssistantDeleteAsync(string assistantId, string apiKey)
    {
        using (LogContext.PushProperty("AssistantId", assistantId))
        {
            _logger.LogDebug("Deleting assistant: {AssistantId}", assistantId);

            var assistant = await _dbContext.Assistants.FindAsync(assistantId);
            if (assistant == null) return false;

            _dbContext.Assistants.Remove(assistant);
            await _dbContext.SaveChangesAsync();

            _logger.LogInformation("Assistant deleted successfully: {AssistantId}", assistantId);
            return true;
        }
    }

    /// <inheritdoc />
    public async Task<ThreadResponse> RouteThreadCreateAsync(ThreadRequest request, string apiKey)
    {
        using (LogContext.PushProperty("Metadata", request.Metadata?.Count ?? 0))
        {
            _logger.LogDebug("Creating thread with {MessageCount} initial messages", request.Messages?.Count ?? 0);

            var threadId = $"thread_{Guid.NewGuid().ToString("N")[..24]}";
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            var thread = new ThreadEntity
            {
                Id = threadId,
                MetadataJson = JsonSerializer.Serialize(request.Metadata ?? new Dictionary<string, string>()),
                CreatedAt = now
            };

            _dbContext.Threads.Add(thread);

            // Add initial messages if provided
            if (request.Messages != null && request.Messages.Count > 0)
                foreach (var msg in request.Messages)
                {
                    var messageId = $"msg_{Guid.NewGuid().ToString("N")[..24]}";
                    var message = new ThreadMessageEntity
                    {
                        Id = messageId,
                        ThreadId = threadId,
                        Role = msg.Role,
                        ContentJson = JsonSerializer.Serialize(msg.Content),
                        FileIdsJson = JsonSerializer.Serialize(msg.FileIds ?? new List<string>()),
                        MetadataJson = JsonSerializer.Serialize(msg.Metadata ?? new Dictionary<string, string>()),
                        CreatedAt = now
                    };
                    _dbContext.ThreadMessages.Add(message);
                }

            await _dbContext.SaveChangesAsync();

            _logger.LogInformation("Thread created successfully: {ThreadId}", threadId);
            return thread.ToThreadResponse();
        }
    }

    /// <inheritdoc />
    public async Task<ThreadResponse?> RouteThreadRetrieveAsync(string threadId, string apiKey)
    {
        using (LogContext.PushProperty("ThreadId", threadId))
        {
            _logger.LogDebug("Retrieving thread: {ThreadId}", threadId);

            var thread = await _dbContext.Threads.FindAsync(threadId);
            return thread?.ToThreadResponse();
        }
    }

    /// <inheritdoc />
    public async Task<ThreadResponse?> RouteThreadUpdateAsync(string threadId, ThreadRequest request, string apiKey)
    {
        using (LogContext.PushProperty("ThreadId", threadId))
        {
            _logger.LogDebug("Updating thread: {ThreadId}", threadId);

            var thread = await _dbContext.Threads.FindAsync(threadId);
            if (thread == null) return null;

            if (request.Metadata != null) thread.MetadataJson = JsonSerializer.Serialize(request.Metadata);

            await _dbContext.SaveChangesAsync();

            _logger.LogInformation("Thread updated successfully: {ThreadId}", threadId);
            return thread.ToThreadResponse();
        }
    }

    /// <inheritdoc />
    public async Task<bool> RouteThreadDeleteAsync(string threadId, string apiKey)
    {
        using (LogContext.PushProperty("ThreadId", threadId))
        {
            _logger.LogDebug("Deleting thread: {ThreadId}", threadId);

            var thread = await _dbContext.Threads.FindAsync(threadId);
            if (thread == null) return false;

            // Also delete associated messages and runs
            var messages = _dbContext.ThreadMessages.Where(m => m.ThreadId == threadId);
            _dbContext.ThreadMessages.RemoveRange(messages);

            var runs = _dbContext.Runs.Where(r => r.ThreadId == threadId);
            _dbContext.Runs.RemoveRange(runs);

            _dbContext.Threads.Remove(thread);
            await _dbContext.SaveChangesAsync();

            _logger.LogInformation("Thread deleted successfully: {ThreadId}", threadId);
            return true;
        }
    }

    /// <inheritdoc />
    public async Task<MessageResponse> RouteMessageCreateAsync(string threadId, MessageRequest request, string apiKey)
    {
        using (LogContext.PushProperty("ThreadId", threadId))
        using (LogContext.PushProperty("Role", request.Role))
        {
            _logger.LogDebug("Creating message in thread: {ThreadId}", threadId);

            var thread = await _dbContext.Threads.FindAsync(threadId);
            if (thread == null) throw new ArgumentException($"Thread not found: {threadId}");

            var messageId = $"msg_{Guid.NewGuid().ToString("N")[..24]}";
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            var message = new ThreadMessageEntity
            {
                Id = messageId,
                ThreadId = threadId,
                Role = request.Role,
                ContentJson = JsonSerializer.Serialize(request.Content),
                FileIdsJson = JsonSerializer.Serialize(request.FileIds ?? new List<string>()),
                MetadataJson = JsonSerializer.Serialize(request.Metadata ?? new Dictionary<string, string>()),
                CreatedAt = now
            };

            _dbContext.ThreadMessages.Add(message);
            await _dbContext.SaveChangesAsync();

            _logger.LogInformation("Message created successfully: {MessageId} in thread: {ThreadId}", messageId,
                                   threadId);
            return message.ToMessageResponse();
        }
    }

    /// <inheritdoc />
    public async Task<MessageListResponse> RouteMessageListAsync(string threadId, int? limit, string? after,
                                                                 string? before, string apiKey)
    {
        using (LogContext.PushProperty("ThreadId", threadId))
        using (LogContext.PushProperty("Limit", limit))
        using (LogContext.PushProperty("After", after))
        using (LogContext.PushProperty("Before", before))
        {
            _logger.LogDebug("Listing messages in thread: {ThreadId}", threadId);

            var query = _dbContext.ThreadMessages.Where(m => m.ThreadId == threadId).OrderByDescending(m => m.CreatedAt)
                                  .AsQueryable();

            if (!string.IsNullOrEmpty(after))
            {
                var afterTime = _dbContext.ThreadMessages.Where(m => m.Id == after).Select(m => m.CreatedAt)
                                          .FirstOrDefault();
                if (afterTime > 0) query = query.Where(m => m.CreatedAt < afterTime);
            }

            if (!string.IsNullOrEmpty(before))
            {
                var beforeTime = _dbContext.ThreadMessages.Where(m => m.Id == before).Select(m => m.CreatedAt)
                                           .FirstOrDefault();
                if (beforeTime > 0) query = query.Where(m => m.CreatedAt > beforeTime);
            }

            var take = limit ?? 20;
            var messages = await query.Take(take + 1).ToListAsync();

            var hasMore = messages.Count > take;
            var data = hasMore ? messages.Take(take).ToList() : messages;

            return new MessageListResponse
            {
                Object = "list",
                Data = data.Select(m => m.ToMessageResponse()).ToList(),
                FirstId = data.FirstOrDefault()?.Id,
                LastId = data.LastOrDefault()?.Id,
                HasMore = hasMore
            };
        }
    }

    /// <inheritdoc />
    public async Task<MessageResponse?> RouteMessageRetrieveAsync(string threadId, string messageId, string apiKey)
    {
        using (LogContext.PushProperty("ThreadId", threadId))
        using (LogContext.PushProperty("MessageId", messageId))
        {
            _logger.LogDebug("Retrieving message: {MessageId} in thread: {ThreadId}", messageId, threadId);

            var message
                = await _dbContext.ThreadMessages.FirstOrDefaultAsync(m => m.Id == messageId && m.ThreadId == threadId);
            return message?.ToMessageResponse();
        }
    }

    /// <inheritdoc />
    public async Task<MessageResponse?> RouteMessageUpdateAsync(string threadId, string messageId,
                                                                MessageRequest request, string apiKey)
    {
        using (LogContext.PushProperty("ThreadId", threadId))
        using (LogContext.PushProperty("MessageId", messageId))
        {
            _logger.LogDebug("Updating message: {MessageId} in thread: {ThreadId}", messageId, threadId);

            var message
                = await _dbContext.ThreadMessages.FirstOrDefaultAsync(m => m.Id == messageId && m.ThreadId == threadId);
            if (message == null) return null;

            if (request.Content != null) message.ContentJson = JsonSerializer.Serialize(request.Content);
            if (request.FileIds != null) message.FileIdsJson = JsonSerializer.Serialize(request.FileIds);
            if (request.Metadata != null) message.MetadataJson = JsonSerializer.Serialize(request.Metadata);

            await _dbContext.SaveChangesAsync();

            _logger.LogInformation("Message updated successfully: {MessageId} in thread: {ThreadId}", messageId,
                                   threadId);
            return message.ToMessageResponse();
        }
    }

    /// <inheritdoc />
    public async Task<RunResponse> RouteRunCreateAsync(string threadId, RunRequest request, string apiKey)
    {
        using (LogContext.PushProperty("ThreadId", threadId))
        using (LogContext.PushProperty("AssistantId", request.AssistantId))
        {
            _logger.LogDebug("Creating run in thread: {ThreadId} with assistant: {AssistantId}", threadId,
                             request.AssistantId);

            var thread = await _dbContext.Threads.FindAsync(threadId);
            if (thread == null) throw new ArgumentException($"Thread not found: {threadId}");

            var assistant = await _dbContext.Assistants.FindAsync(request.AssistantId);
            if (assistant == null) throw new ArgumentException($"Assistant not found: {request.AssistantId}");

            var runId = $"run_{Guid.NewGuid().ToString("N")[..24]}";
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            var run = new RunEntity
            {
                Id = runId,
                ThreadId = threadId,
                AssistantId = request.AssistantId,
                Model = request.Model ?? assistant.Model,
                Instructions = request.Instructions ?? assistant.Instructions,
                ToolsJson = request.Tools != null ? JsonSerializer.Serialize(request.Tools) : assistant.ToolsJson,
                FileIdsJson = JsonSerializer.Serialize(request.FileIds ?? new List<string>()),
                MetadataJson = JsonSerializer.Serialize(request.Metadata ?? new Dictionary<string, string>()),
                Status = "queued",
                ResponseFormatJson = JsonSerializer.Serialize(request.ResponseFormat ?? new { }),
                ToolChoiceJson = JsonSerializer.Serialize(request.ToolChoice ?? new { }),
                TruncationStrategyJson = request.TruncationStrategy != null
                    ? JsonSerializer.Serialize(request.TruncationStrategy)
                    : "{}",
                CreatedAt = now,
                ExpiresAt = now + 1800 // 30 minutes default
            };

            _dbContext.Runs.Add(run);
            await _dbContext.SaveChangesAsync();

            // Process the run asynchronously (in a real implementation, this would be a background job)
            _ = Task.Run(async () => await ProcessRunAsync(runId, apiKey));

            _logger.LogInformation("Run created successfully: {RunId} in thread: {ThreadId}", runId, threadId);
            return run.ToRunResponse();
        }
    }

    /// <inheritdoc />
    public async Task<RunResponse?> RouteRunRetrieveAsync(string threadId, string runId, string apiKey)
    {
        using (LogContext.PushProperty("ThreadId", threadId))
        using (LogContext.PushProperty("RunId", runId))
        {
            _logger.LogDebug("Retrieving run: {RunId} in thread: {ThreadId}", runId, threadId);

            var run = await _dbContext.Runs.FirstOrDefaultAsync(r => r.Id == runId && r.ThreadId == threadId);
            return run?.ToRunResponse();
        }
    }

    /// <inheritdoc />
    public async Task<RunResponse?> RouteRunUpdateAsync(string threadId, string runId, RunRequest request,
                                                        string apiKey)
    {
        using (LogContext.PushProperty("ThreadId", threadId))
        using (LogContext.PushProperty("RunId", runId))
        {
            _logger.LogDebug("Updating run: {RunId} in thread: {ThreadId}", runId, threadId);

            var run = await _dbContext.Runs.FirstOrDefaultAsync(r => r.Id == runId && r.ThreadId == threadId);
            if (run == null) return null;

            if (!string.IsNullOrEmpty(request.Model)) run.Model = request.Model;
            if (!string.IsNullOrEmpty(request.Instructions)) run.Instructions = request.Instructions;
            if (request.Tools != null) run.ToolsJson = JsonSerializer.Serialize(request.Tools);
            if (request.FileIds != null) run.FileIdsJson = JsonSerializer.Serialize(request.FileIds);
            if (request.Metadata != null) run.MetadataJson = JsonSerializer.Serialize(request.Metadata);
            if (request.ResponseFormat != null)
                run.ResponseFormatJson = JsonSerializer.Serialize(request.ResponseFormat);
            if (request.ToolChoice != null) run.ToolChoiceJson = JsonSerializer.Serialize(request.ToolChoice);
            if (request.TruncationStrategy != null)
                run.TruncationStrategyJson = JsonSerializer.Serialize(request.TruncationStrategy);

            await _dbContext.SaveChangesAsync();

            _logger.LogInformation("Run updated successfully: {RunId} in thread: {ThreadId}", runId, threadId);
            return run.ToRunResponse();
        }
    }

    /// <inheritdoc />
    public async Task<RunResponse?> RouteRunCancelAsync(string threadId, string runId, string apiKey)
    {
        using (LogContext.PushProperty("ThreadId", threadId))
        using (LogContext.PushProperty("RunId", runId))
        {
            _logger.LogDebug("Cancelling run: {RunId} in thread: {ThreadId}", runId, threadId);

            var run = await _dbContext.Runs.FirstOrDefaultAsync(r => r.Id == runId && r.ThreadId == threadId);
            if (run == null) return null;

            if (run.Status == "completed" || run.Status == "failed" || run.Status == "cancelled"
                    || run.Status == "expired")
                // Already in terminal state
                return run.ToRunResponse();

            run.Status = "cancelling";
            await _dbContext.SaveChangesAsync();

            // In a real implementation, we would signal the background processing to stop
            // For now, just mark as cancelled
            run.Status = "cancelled";
            run.CompletedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            await _dbContext.SaveChangesAsync();

            _logger.LogInformation("Run cancelled: {RunId} in thread: {ThreadId}", runId, threadId);
            return run.ToRunResponse();
        }
    }

    /// <inheritdoc />
    public async Task<RunResponse?> RouteRunSubmitToolOutputsAsync(string threadId, string runId,
                                                                   SubmitToolOutputsRequest request, string apiKey)
    {
        using (LogContext.PushProperty("ThreadId", threadId))
        using (LogContext.PushProperty("RunId", runId))
        {
            _logger.LogDebug("Submitting tool outputs for run: {RunId} in thread: {ThreadId}", runId, threadId);

            var run = await _dbContext.Runs.FirstOrDefaultAsync(r => r.Id == runId && r.ThreadId == threadId);
            if (run == null) return null;

            if (run.Status != "requires_action")
                throw new InvalidOperationException($"Run is not in requires_action state: {run.Status}");

            // Process tool outputs and continue the run
            // In a real implementation, this would resume the assistant's execution
            run.Status = "in_progress";
            run.RequiredActionJson = "{}";
            await _dbContext.SaveChangesAsync();

            // Continue processing the run
            _ = Task.Run(async () => await ProcessRunAsync(runId, apiKey));

            _logger.LogInformation("Tool outputs submitted for run: {RunId} in thread: {ThreadId}", runId, threadId);
            return run.ToRunResponse();
        }
    }

    /// <inheritdoc />
    public async Task<RunListResponse> RouteRunListAsync(string threadId, int? limit, string? after, string apiKey)
    {
        using (LogContext.PushProperty("ThreadId", threadId))
        using (LogContext.PushProperty("Limit", limit))
        using (LogContext.PushProperty("After", after))
        {
            _logger.LogDebug("Listing runs in thread: {ThreadId}", threadId);

            var query = _dbContext.Runs.Where(r => r.ThreadId == threadId).OrderByDescending(r => r.CreatedAt)
                                  .AsQueryable();

            if (!string.IsNullOrEmpty(after))
            {
                var afterTime = _dbContext.Runs.Where(r => r.Id == after).Select(r => r.CreatedAt).FirstOrDefault();
                if (afterTime > 0) query = query.Where(r => r.CreatedAt < afterTime);
            }

            var take = limit ?? 20;
            var runs = await query.Take(take + 1).ToListAsync();

            var hasMore = runs.Count > take;
            var data = hasMore ? runs.Take(take).ToList() : runs;

            return new RunListResponse
            {
                Object = "list",
                Data = data.Select(r => r.ToRunResponse()).ToList(),
                FirstId = data.FirstOrDefault()?.Id,
                LastId = data.LastOrDefault()?.Id,
                HasMore = hasMore
            };
        }
    }

    /// <inheritdoc />
    public async Task<RunStepResponse?> RouteRunStepRetrieveAsync(string threadId, string runId, string stepId,
                                                                  string apiKey)
    {
        using (LogContext.PushProperty("ThreadId", threadId))
        using (LogContext.PushProperty("RunId", runId))
        using (LogContext.PushProperty("StepId", stepId))
        {
            _logger.LogDebug("Retrieving run step: {StepId} in run: {RunId}", stepId, runId);

            var step = await _dbContext.RunSteps.FirstOrDefaultAsync(s => s.Id == stepId && s.RunId == runId
                                                                         && s.ThreadId == threadId);
            return step?.ToRunStepResponse();
        }
    }

    /// <inheritdoc />
    public async Task<RunStepListResponse> RouteRunStepListAsync(string threadId, string runId, int? limit,
                                                                 string? after, string apiKey)
    {
        using (LogContext.PushProperty("ThreadId", threadId))
        using (LogContext.PushProperty("RunId", runId))
        using (LogContext.PushProperty("Limit", limit))
        using (LogContext.PushProperty("After", after))
        {
            _logger.LogDebug("Listing run steps for run: {RunId} in thread: {ThreadId}", runId, threadId);

            var query = _dbContext.RunSteps.Where(s => s.RunId == runId && s.ThreadId == threadId)
                                  .OrderBy(s => s.CreatedAt).AsQueryable();

            if (!string.IsNullOrEmpty(after))
            {
                var afterTime = _dbContext.RunSteps.Where(s => s.Id == after).Select(s => s.CreatedAt).FirstOrDefault();
                if (afterTime > 0) query = query.Where(s => s.CreatedAt > afterTime);
            }

            var take = limit ?? 20;
            var steps = await query.Take(take + 1).ToListAsync();

            var hasMore = steps.Count > take;
            var data = hasMore ? steps.Take(take).ToList() : steps;

            return new RunStepListResponse
            {
                Object = "list",
                Data = data.Select(s => s.ToRunStepResponse()).ToList(),
                FirstId = data.FirstOrDefault()?.Id,
                LastId = data.LastOrDefault()?.Id,
                HasMore = hasMore
            };
        }
    }

    // ==================== ADMIN/ORGANIZATION API IMPLEMENTATION ====================

    /// <inheritdoc />
    public async Task<ApiKeyResponse> RouteApiKeyCreateAsync(ApiKeyRequest request, string apiKey)
    {
        using (LogContext.PushProperty("Name", request.Name))
        using (LogContext.PushProperty("Role", request.Role))
        {
            _logger.LogDebug("Creating API key: name={Name}, role={Role}", request.Name, request.Role);

            var keyId = $"sk_{Guid.NewGuid().ToString("N")[..32]}";
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var apiKeyValue = $"sk-proj-{Guid.NewGuid().ToString("N")}{Guid.NewGuid().ToString("N")[..16]}";

            // In a real implementation, this would store in database
            // For now, return mock response
            return new ApiKeyResponse
            {
                Id = keyId,
                Object = "api_key",
                Name = request.Name,
                Role = request.Role,
                Key = apiKeyValue,
                CreatedAt = now,
                ExpiresAt = null,
                Revoked = false
            };
        }
    }

    /// <inheritdoc />
    public async Task<ApiKeyListResponse> RouteApiKeyListAsync(int? limit, string? after, string apiKey)
    {
        using (LogContext.PushProperty("Limit", limit))
        using (LogContext.PushProperty("After", after))
        {
            _logger.LogDebug("Listing API keys with limit={Limit}, after={After}", limit, after);

            // Mock response - in real implementation, fetch from database
            return new ApiKeyListResponse
            {
                Object = "list",
                Data = new List<ApiKeyResponse>(),
                HasMore = false
            };
        }
    }

    /// <inheritdoc />
    public async Task<ApiKeyResponse?> RouteApiKeyRetrieveAsync(string keyId, string apiKey)
    {
        using (LogContext.PushProperty("KeyId", keyId))
        {
            _logger.LogDebug("Retrieving API key: {KeyId}", keyId);
            return null; // Not found in mock
        }
    }

    /// <inheritdoc />
    public async Task<bool> RouteApiKeyDeleteAsync(string keyId, string apiKey)
    {
        using (LogContext.PushProperty("KeyId", keyId))
        {
            _logger.LogDebug("Deleting API key: {KeyId}", keyId);
            return false; // Not found in mock
        }
    }

    /// <inheritdoc />
    public async Task<ProjectResponse> RouteProjectCreateAsync(ProjectRequest request, string apiKey)
    {
        using (LogContext.PushProperty("Name", request.Name))
        {
            _logger.LogDebug("Creating project: name={Name}", request.Name);

            var projectId = $"proj_{Guid.NewGuid().ToString("N")[..24]}";
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            return new ProjectResponse
            {
                Id = projectId,
                Object = "project",
                Name = request.Name,
                Description = request.Description,
                CreatedAt = now,
                Archived = false,
                ArchivedAt = null
            };
        }
    }

    /// <inheritdoc />
    public async Task<ProjectListResponse> RouteProjectListAsync(int? limit, string? after, string apiKey)
    {
        using (LogContext.PushProperty("Limit", limit))
        using (LogContext.PushProperty("After", after))
        {
            _logger.LogDebug("Listing projects with limit={Limit}, after={After}", limit, after);

            return new ProjectListResponse
            {
                Object = "list",
                Data = new List<ProjectResponse>(),
                HasMore = false
            };
        }
    }

    /// <inheritdoc />
    public async Task<ProjectResponse?> RouteProjectRetrieveAsync(string projectId, string apiKey)
    {
        using (LogContext.PushProperty("ProjectId", projectId))
        {
            _logger.LogDebug("Retrieving project: {ProjectId}", projectId);
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<ProjectResponse?> RouteProjectUpdateAsync(string projectId, ProjectRequest request, string apiKey)
    {
        using (LogContext.PushProperty("ProjectId", projectId))
        {
            _logger.LogDebug("Updating project: {ProjectId}", projectId);
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<ProjectResponse?> RouteProjectArchiveAsync(string projectId, string apiKey)
    {
        using (LogContext.PushProperty("ProjectId", projectId))
        {
            _logger.LogDebug("Archiving project: {ProjectId}", projectId);
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<UserListResponse> RouteUserListAsync(int? limit, string? after, string apiKey)
    {
        using (LogContext.PushProperty("Limit", limit))
        using (LogContext.PushProperty("After", after))
        {
            _logger.LogDebug("Listing users with limit={Limit}, after={After}", limit, after);

            return new UserListResponse
            {
                Object = "list",
                Data = new List<UserResponse>
                {
                    new()
                    {
                        Id = "user_1",
                        Object = "user",
                        Name = "Owner",
                        Email = "admin@example.com",
                        Role = "owner",
                        AddedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                        IsOwner = true
                    }
                },
                HasMore = false
            };
        }
    }

    /// <inheritdoc />
    public async Task<UserResponse?> RouteUserRetrieveAsync(string userId, string apiKey)
    {
        using (LogContext.PushProperty("UserId", userId))
        {
            _logger.LogDebug("Retrieving user: {UserId}", userId);
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<InviteResponse> RouteInviteCreateAsync(InviteRequest request, string apiKey)
    {
        using (LogContext.PushProperty("Email", request.Email))
        using (LogContext.PushProperty("Role", request.Role))
        {
            _logger.LogDebug("Creating invite: email={Email}, role={Role}", request.Email, request.Role);

            var inviteId = $"invite_{Guid.NewGuid().ToString("N")[..24]}";
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            return new InviteResponse
            {
                Id = inviteId,
                Object = "invite",
                Email = request.Email,
                Role = request.Role,
                ProjectId = request.ProjectId,
                CreatedAt = now,
                ExpiresAt = now + 604800, // 7 days
                Status = "pending"
            };
        }
    }

    /// <inheritdoc />
    public async Task<InviteListResponse> RouteInviteListAsync(int? limit, string? after, string apiKey)
    {
        using (LogContext.PushProperty("Limit", limit))
        using (LogContext.PushProperty("After", after))
        {
            _logger.LogDebug("Listing invites with limit={Limit}, after={After}", limit, after);

            return new InviteListResponse
            {
                Object = "list",
                Data = new List<InviteResponse>(),
                HasMore = false
            };
        }
    }

    /// <inheritdoc />
    public async Task<bool> RouteInviteDeleteAsync(string inviteId, string apiKey)
    {
        using (LogContext.PushProperty("InviteId", inviteId))
        {
            _logger.LogDebug("Deleting invite: {InviteId}", inviteId);
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<AuditLogListResponse> RouteAuditLogListAsync(int? limit, string? after, string apiKey)
    {
        using (LogContext.PushProperty("Limit", limit))
        using (LogContext.PushProperty("After", after))
        {
            _logger.LogDebug("Listing audit logs with limit={Limit}, after={After}", limit, after);

            return new AuditLogListResponse
            {
                Object = "list",
                Data = new List<AuditLogEntry>(),
                HasMore = false
            };
        }
    }

    /// <inheritdoc />
    public async Task<OrganizationResponse?> RouteOrganizationRetrieveAsync(string apiKey)
    {
        _logger.LogDebug("Retrieving organization");

        return new OrganizationResponse
        {
            Id = "org_1",
            Object = "organization",
            Name = "VanPatten AI Router",
            CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            DefaultProjectId = null
        };
    }

    /// <summary>
    ///     Processes a run asynchronously (background job).
    /// </summary>
    private async Task ProcessRunAsync(string runId, string apiKey)
    {
        try
        {
            using (LogContext.PushProperty("RunId", runId))
            {
                _logger.LogDebug("Starting background processing for run: {RunId}", runId);

                // Get the run
                var run = await _dbContext.Runs
                                          .FirstOrDefaultAsync(r => r.Id == runId);

                if (run == null)
                {
                    _logger.LogWarning("Run not found for processing: {RunId}", runId);
                    return;
                }

                // Update status to in_progress
                run.Status = "in_progress";
                run.StartedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                await _dbContext.SaveChangesAsync();

                // Get thread messages for context
                var messages = await _dbContext.ThreadMessages
                                               .Where(m => m.ThreadId == run.ThreadId)
                                               .OrderBy(m => m.CreatedAt)
                                               .ToListAsync();

                // Convert to ChatCompletionRequest format
                var chatRequest = new ChatCompletionRequest
                {
                    Model = run.Model,
                    Messages = messages.Select(m => new ChatMessage
                    {
                        Role = m.Role,
                        Content = m.ContentJson
                    }).ToList(),
                    Tools = string.IsNullOrEmpty(run.ToolsJson)
                        ? new List<Tool>()
                        : JsonSerializer.Deserialize<List<Tool>>(run.ToolsJson) ?? new List<Tool>(),
                    Temperature = 1.0,
                    MaxTokens = 4096
                };

                // Create a mock output stream for the chat completion
                using var memoryStream = new MemoryStream();
                await RouteChatCompletionStreamAsync(chatRequest, apiKey, memoryStream, CancellationToken.None);

                // In a real implementation, we would parse the streaming response,
                // handle tool calls, create messages, etc.
                // For now, just mark as completed with a mock response

                var assistantMessageId = $"msg_{Guid.NewGuid().ToString("N")[..24]}";
                var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

                var assistantMessage = new ThreadMessageEntity
                {
                    Id = assistantMessageId,
                    ThreadId = run.ThreadId,
                    Role = "assistant",
                    ContentJson = JsonSerializer.Serialize("I'm an AI assistant. How can I help you?"),
                    FileIdsJson = "[]",
                    MetadataJson = "{}",
                    AssistantId = run.AssistantId,
                    RunId = runId,
                    CreatedAt = now
                };

                _dbContext.ThreadMessages.Add(assistantMessage);

                // Create a run step for the message creation
                var stepId = $"step_{Guid.NewGuid().ToString("N")[..24]}";
                var runStep = new RunStepEntity
                {
                    Id = stepId,
                    RunId = run.Id,
                    ThreadId = run.ThreadId,
                    AssistantId = run.AssistantId,
                    Type = "message_creation",
                    Status = "completed",
                    StepDetailsJson = JsonSerializer.Serialize(new RunStepDetails
                    {
                        Type = "message_creation",
                        MessageCreation = new MessageCreationDetails { MessageId = assistantMessageId }
                    }),
                    StartedAt = run.StartedAt,
                    CompletedAt = now,
                    CreatedAt = now
                };

                _dbContext.RunSteps.Add(runStep);

                // Mark run as completed
                run.Status = "completed";
                run.CompletedAt = now;
                run.UsageJson = JsonSerializer.Serialize(new Usage
                {
                    PromptTokens = 100,
                    CompletionTokens = 50,
                    TotalTokens = 150
                });

                await _dbContext.SaveChangesAsync();

                _logger.LogInformation("Run completed successfully: {RunId}", runId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing run: {RunId}", runId);

            // Mark run as failed
            try
            {
                var run = await _dbContext.Runs.FindAsync(runId);
                if (run != null)
                {
                    run.Status = "failed";
                    run.FailedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                    run.LastErrorJson = JsonSerializer.Serialize(new RunError
                    {
                        Code = "server_error",
                        Message = ex.Message
                    });
                    await _dbContext.SaveChangesAsync();
                }
            }
            catch
            {
                // Ignore errors in error handling
            }
        }
    }

    /// <summary>
    ///     Gets the error action for a given HTTP status code.
    /// </summary>
    /// <param name="statusCode">The HTTP status code.</param>
    /// <returns>The error action.</returns>
    private static ProviderErrorAction GetErrorAction(int statusCode)
    {
        return statusCode switch
        {
            400 => ProviderErrorAction.Failover,
            401 => ProviderErrorAction.Failover,
            402 => ProviderErrorAction.Failover,
            403 => ProviderErrorAction.Failover,
            404 => ProviderErrorAction.Failover,
            429 => ProviderErrorAction.Retry,
            500 => ProviderErrorAction.Retry,
            502 => ProviderErrorAction.Retry,
            503 => ProviderErrorAction.Retry,
            504 => ProviderErrorAction.Retry,
            _ => statusCode >= 500 ? ProviderErrorAction.Retry : ProviderErrorAction.Failover
        };
    }

    /// <summary>
    ///     Gets a human-readable description for an HTTP status code error.
    /// </summary>
    private static string GetErrorDescription(int statusCode)
    {
        return statusCode switch
        {
            400 => "Bad Request - Invalid request format or parameters",
            401 => "Unauthorized - Invalid or missing API key",
            402 => "Payment Required - Credits exhausted or paid model with no credits",
            403 => "Forbidden - Access denied by provider",
            404 => "Not Found - Model or endpoint not found",
            429 => "Rate Limited - Too many requests, try again later",
            500 => "Internal Server Error - Provider-side issue",
            502 => "Bad Gateway - Upstream provider error",
            503 => "Service Unavailable - Provider temporarily overloaded",
            504 => "Gateway Timeout - Provider did not respond in time",
            _ => $"HTTP {statusCode} - Provider returned an error"
        };
    }

    /// <summary>
    ///     Creates an HTTP client configured for streaming (no timeout).
    /// </summary>
    private HttpClient CreateStreamingClient()
    {
        return _httpClientFactory.CreateClient("streaming");
    }

    /// <summary>
    ///     Creates an HTTP client with default timeout for non-streaming requests.
    /// </summary>
    private HttpClient CreateDefaultClient()
    {
        return _httpClientFactory.CreateClient("default");
    }

    /// <summary>
    ///     Configures an HTTP client for a provider (base address, auth, headers).
    /// </summary>
    private void ConfigureClientForProvider(HttpClient client, Provider provider)
    {
        client.BaseAddress = new Uri(provider.BaseUrl.TrimEnd('/') + "/");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", provider.ApiKey);

        var headers = ParseHeaders(provider.Headers);
        foreach (var header in headers) client.DefaultRequestHeaders.TryAddWithoutValidation(header.Key, header.Value);
    }

    /// <summary>
    ///     Routes a request through a virtual provider (e.g., VanPatten/auto-free, VanPatten/auto) by trying models from all
    ///     providers.
    /// </summary>
    /// <param name="virtualProvider">The virtual provider.</param>
    /// <param name="request">The chat completion request.</param>
    /// <param name="apiKey">The API key.</param>
    /// <param name="estimatedTokens">Estimated token count.</param>
    /// <param name="stopwatch">The stopwatch for timing.</param>
    /// <param name="specificModel">The specific model name for "specific" mode, or null for auto/auto-free modes.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The chat completion response.</returns>
    private async Task<ChatCompletionResponse> RouteVirtualProviderAsync(
        Provider virtualProvider,
        ChatCompletionRequest request,
        string apiKey,
        int estimatedTokens,
        Stopwatch stopwatch,
        string mode,
        string? specificModel,
        CancellationToken cancellationToken)
    {
        List<ProviderModel> models;
        string modeDescription;

        if (mode == "specific" && !string.IsNullOrEmpty(specificModel))
        {
            // Third mode: specific model with failover to same model on other providers
            models = await _providerService.GetProvidersForSpecificModelAsync(specificModel, virtualProvider.Name);
            modeDescription = $"specific model '{specificModel}'";
        }
        else if (mode == "auto")
        {
            models = await _providerService.GetAllModelsForAutoModeAsync(virtualProvider.Name);
            modeDescription = "auto (all models)";
        }
        else // auto-free (default)
        {
            models = await _providerService.GetFreeModelsForAutoModeAsync(virtualProvider.Name);
            modeDescription = "auto-free (free models only)";
        }

        if (models.Count == 0)
        {
            _logger.LogWarning("No models available for virtual provider {VirtualProvider} (mode: {Mode})",
                               virtualProvider.Name, modeDescription);
            return new ChatCompletionResponse
            {
                Model = request.Model,
                Choices = new List<ChatChoice>
                {
                    new()
                    {
                        Index = 0,
                        Message = new ChatMessage
                        {
                            Role = "assistant",
                            Content = $"No models available for {modeDescription}."
                        },
                        FinishReason = "rate_limit"
                    }
                },
                Usage = new Usage()
            };
        }

        _logger.LogInformation(
            "Virtual provider {VirtualProvider} routing (mode: {Mode}): {ModelCount} models available",
            virtualProvider.Name, modeDescription, models.Count);

        // Group models by provider
        var modelsByProvider = models
                              .GroupBy(m => m.Provider)
                              .OrderByDescending(g => g.Key.Priority)
                              .ToList();

        // Try each provider in priority order
        Exception? lastException = null;
        string? lastErrorMessage = null;
        foreach (var providerGroup in modelsByProvider)
        {
            var provider = providerGroup.Key;
            var providerModels = providerGroup.ToList();

            // Check rate limits for this provider
            if (provider.IsRateLimited)
            {
                _logger.LogDebug("Skipping rate-limited provider: {ProviderName}", provider.Name);
                continue;
            }

            // For specific mode, we only have one model per provider (the specific one)
            // For auto/auto-free, we try each model from this provider
            var modelsToTry = mode == "specific"
                ? providerModels.Where(m => m.ModelName == specificModel).ToList()
                : providerModels;

            foreach (var model in modelsToTry)
                using (LogContext.PushProperty("Provider", provider.Name))
                using (LogContext.PushProperty("ProviderId", provider.Id))
                using (LogContext.PushProperty("ActualModel", model.ModelName))
                {
                    _logger.LogInformation(
                        "Trying model {ModelName} from provider {ProviderName} (priority: {Priority})",
                        model.ModelName, provider.Name, provider.Priority);

                    ChatCompletionResponse? result;
                    try
                    {
                        result = await TryProviderWithModelAsync(provider, model.ModelName, request, apiKey,
                                                                 estimatedTokens, cancellationToken);
                    }
                    catch (ProviderFailoverException ex)
                    {
                        lastException = ex;
                        lastErrorMessage = ex.Message;
                        _logger.LogWarning(
                            "Model {ModelName} from provider {ProviderName} failed, trying next (reason: {Reason})",
                            model.ModelName, provider.Name, ex.Message);
                        continue;
                    }
                    catch (Exception ex)
                    {
                        lastException = ex;
                        _logger.LogWarning(
                            "Model {ModelName} from provider {ProviderName} failed, trying next (reason: {Reason})",
                            model.ModelName, provider.Name, ex.Message);
                        continue;
                    }

                    if (result != null)
                    {
                        stopwatch.Stop();
                        // Update the model name in response to show what was actually used
                        result.Model = $"{virtualProvider.Name}/{mode} ({provider.Name}/{model.ModelName})";
                        _logger.LogInformation(
                            "Virtual provider {VirtualProvider} succeeded with {Provider}/{Model} (elapsed: {Elapsed}ms)",
                            virtualProvider.Name, provider.Name, model.ModelName, stopwatch.ElapsedMilliseconds);
                        return result;
                    }
                }
        }

        // All providers failed
        stopwatch.Stop();
        _logger.LogError("Virtual provider {VirtualProvider} (mode: {Mode}) failed - all {Count} providers exhausted",
                         virtualProvider.Name, modeDescription, modelsByProvider.Count);

        return new ChatCompletionResponse
        {
            Model = request.Model,
            SystemFingerprint = "fp_error",
            ServiceTier = request.ServiceTier ?? "default",
            Choices = new List<ChatChoice>
            {
                new()
                {
                    Index = 0,
                    Message = new ChatMessage
                    {
                        Role = "assistant",
                        Content
                            = $"All models exhausted for {modeDescription}. Last error: {lastErrorMessage ?? lastException?.Message ?? "Unknown error"}"
                    },
                    FinishReason = "error"
                }
            },
            Usage = new Usage()
        };
    }

    private async Task<ChatCompletionResponse?> TryProviderWithModelAsync(
        Provider provider,
        string actualModelName,
        ChatCompletionRequest request,
        string apiKey,
        int estimatedTokens,
        CancellationToken cancellationToken)
    {
        var retryPolicy = _retryPolicyService.GetRetryPolicy<ChatCompletionResponse>();

        return await retryPolicy.ExecuteAsync(async ctx =>
        {
            var providerStopwatch = Stopwatch.StartNew();
            var connectionStopwatch = Stopwatch.StartNew();
            var disconnectReason = "Unknown";

            try
            {
                var client = CreateDefaultClient();
                ConfigureClientForProvider(client, provider);

                _logger.LogDebug(
                    "Initiating non-streaming request to provider {Provider} model {Model} (BaseUrl: {BaseUrl})",
                    provider.Name, actualModelName, provider.BaseUrl);

                var requestBody = BuildRequestBody(request, actualModelName);

                var json = JsonSerializer.Serialize(requestBody, new JsonSerializerOptions
                {
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });

                var content = new StringContent(json, Encoding.UTF8, "application/json");

                _logger.LogDebug("Sending non-streaming request to {Provider} ({Model}) - Request size: {Size} bytes",
                                 provider.Name, actualModelName, json.Length);

                using var requestMsg = new HttpRequestMessage(HttpMethod.Post, "chat/completions")
                {
                    Content = content
                };

                // Send request and measure connection establishment time
                using var response
                    = await client.SendAsync(requestMsg, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

                connectionStopwatch.Stop();

                _logger.LogDebug(
                    "HTTP response headers received from {Provider} ({Model}) - Status: {StatusCode}, Connection time: {ConnTime}ms",
                    provider.Name, actualModelName, response.StatusCode, connectionStopwatch.ElapsedMilliseconds);

                var responseString = await response.Content.ReadAsStringAsync(cancellationToken);

                providerStopwatch.Stop();

                if (!response.IsSuccessStatusCode)
                {
                    disconnectReason = $"HTTP {(int)response.StatusCode} {response.StatusCode}";
                    var errorDescription = GetErrorDescription((int)response.StatusCode);
                    _logger.LogError(
                        "Provider {Provider} returned error: {ErrorDescription} - {Response} (elapsed: {Elapsed}ms, connection: {ConnTime}ms)",
                        provider.Name, errorDescription, responseString, providerStopwatch.ElapsedMilliseconds,
                        connectionStopwatch.ElapsedMilliseconds);

                    var errorAction = GetErrorAction((int)response.StatusCode);

                    if (errorAction == ProviderErrorAction.Failover)
                    {
                        // Non-retryable error - throw failover exception with error message
                        _logger.LogInformation(
                            "Provider {Provider} error {StatusCode} ({ErrorDescription}) is non-retryable, failing over. Disconnect reason: {Reason}",
                            provider.Name, response.StatusCode, errorDescription, disconnectReason);
                        throw new ProviderFailoverException(
                            $"Provider {provider.Name} returned {(int)response.StatusCode}: {responseString}");
                    }

                    // Retryable error - throw to trigger Polly retry
                    _logger.LogWarning(
                        "Provider {Provider} error {StatusCode} ({ErrorDescription}) is retryable, will retry. Disconnect reason: {Reason}",
                        provider.Name, response.StatusCode, errorDescription, disconnectReason);
                    throw new HttpRequestException(
                        $"Provider {provider.Name} returned {(int)response.StatusCode}: {responseString}");
                }

                var completionResponse = JsonSerializer.Deserialize<ChatCompletionResponse>(
                    responseString, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true,
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                    });

                if (completionResponse != null)
                    completionResponse.Model = request.Model; // Return the requested model name

                // Add OpenAI-compatible fields
                if (completionResponse != null)
                {
                    completionResponse.SystemFingerprint = GenerateSystemFingerprint(provider.Name, actualModelName);
                    completionResponse.ServiceTier = request.ServiceTier ?? "default";
                }

                // Record usage
                var tokensUsed = completionResponse?.Usage?.TotalTokens ?? estimatedTokens;
                await _providerService.RecordUsageAsync(provider.Id, tokensUsed);

                _logger.LogInformation(
                    "Successfully completed request with provider {Provider} model {Model} (elapsed: {Elapsed}ms, connection: {ConnTime}ms, tokens: {TotalTokens})",
                    provider.Name, actualModelName, providerStopwatch.ElapsedMilliseconds,
                    connectionStopwatch.ElapsedMilliseconds, tokensUsed);

                return completionResponse ?? new ChatCompletionResponse { Model = request.Model };
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                _logger.LogInformation("Request cancelled for model {Model} after {Elapsed}ms", request.Model,
                                       providerStopwatch.ElapsedMilliseconds);
                throw;
            }
            catch (ProviderFailoverException)
            {
                throw; // Don't wrap failover exceptions
            }
            catch (HttpRequestException ex) when (ex.InnerException is SocketException socketEx)
            {
                disconnectReason = $"Socket error: {socketEx.SocketErrorCode} - {socketEx.Message}";
                _logger.LogError(ex,
                                 "SOCKET ERROR - Provider {Provider} model {Model} failed: SocketErrorCode={SocketErrorCode}, Message={Message}, NativeErrorCode={NativeErrorCode}, Elapsed={Elapsed}ms",
                                 provider.Name, actualModelName, socketEx.SocketErrorCode, socketEx.Message,
                                 socketEx.NativeErrorCode, providerStopwatch.ElapsedMilliseconds);
                throw;
            }
            catch (HttpRequestException ex)
            {
                disconnectReason = $"HTTP request error: {ex.Message}";
                _logger.LogError(ex,
                                 "HTTP REQUEST ERROR - Provider {Provider} model {Model} failed: {Message}, StatusCode={StatusCode}, Elapsed={Elapsed}ms",
                                 provider.Name, actualModelName, ex.Message, ex.StatusCode,
                                 providerStopwatch.ElapsedMilliseconds);
                throw;
            }
            catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
            {
                disconnectReason = $"Request timeout (TaskCanceledException): {ex.Message}";
                _logger.LogError(ex,
                                 "TIMEOUT - Provider {Provider} model {Model} request timed out after {Elapsed}ms. InnerException: {InnerEx}",
                                 provider.Name, actualModelName, providerStopwatch.ElapsedMilliseconds,
                                 ex.InnerException?.Message);
                throw;
            }
            catch (IOException ex)
            {
                disconnectReason = $"IO error: {ex.Message}";
                _logger.LogError(ex,
                                 "IO ERROR - Provider {Provider} model {Model} failed: {Message}, HResult=0x{HResult:X}, Elapsed={Elapsed}ms",
                                 provider.Name, actualModelName, ex.Message, ex.HResult,
                                 providerStopwatch.ElapsedMilliseconds);
                throw;
            }
            catch (Exception ex)
            {
                disconnectReason = $"Unexpected error: {ex.GetType().Name}: {ex.Message}";
                _logger.LogError(ex,
                                 "UNEXPECTED ERROR - Provider {Provider} model {Model} failed: {ExceptionType}: {Message}, Elapsed={Elapsed}ms",
                                 provider.Name, actualModelName, ex.GetType().Name, ex.Message,
                                 providerStopwatch.ElapsedMilliseconds);
                throw;
            }
        }, cancellationToken);
    }

    /// <summary>
    ///     Routes a chat completion request to a single provider with failover to other providers.
    /// </summary>
    /// <param name="provider">The primary provider.</param>
    /// <param name="request">The chat completion request.</param>
    /// <param name="apiKey">The API key.</param>
    /// <param name="estimatedTokens">Estimated token count.</param>
    /// <param name="stopwatch">The stopwatch for timing.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The chat completion response.</returns>
    private async Task<ChatCompletionResponse> RouteToSingleProviderAsync(
        Provider provider,
        ChatCompletionRequest request,
        string apiKey,
        int estimatedTokens,
        Stopwatch stopwatch,
        CancellationToken cancellationToken)
    {
        // Get ALL available providers for this model (in priority order)
        var allProviders = await _providerService.GetAllAvailableProvidersForModelAsync(request.Model, estimatedTokens);

        if (allProviders.Count == 0)
        {
            stopwatch.Stop();
            _logger.LogWarning("No available providers for model: {Model}", request.Model);
            return new ChatCompletionResponse
            {
                Model = request.Model,
                Choices = new List<ChatChoice>
                {
                    new()
                    {
                        Index = 0,
                        Message = new ChatMessage
                        {
                            Role = "assistant",
                            Content
                                = $"No available provider for model '{request.Model}'. All providers are rate limited or not configured."
                        },
                        FinishReason = "rate_limit"
                    }
                },
                Usage = new Usage()
            };
        }

        Exception? lastException = null;
        string? lastErrorMessage = null;

        // Try each provider in priority order until one succeeds
        foreach (var p in allProviders)
            using (LogContext.PushProperty("Provider", p.Name))
            using (LogContext.PushProperty("ProviderId", p.Id))
            {
                _logger.LogInformation("Trying provider {Provider} for model {Model} (priority: {Priority})",
                                       p.Name, request.Model, p.Priority);

                try
                {
                    var result = await TryProviderWithModelAsync(p, request.Model, request, apiKey,
                                                                 estimatedTokens, cancellationToken);

                    if (result != null)
                    {
                        stopwatch.Stop();
                        _logger.LogInformation(
                            "Successfully completed request with provider {Provider} (elapsed: {Elapsed}ms)",
                            p.Name, stopwatch.ElapsedMilliseconds);
                        return result;
                    }
                }
                catch (ProviderFailoverException ex)
                {
                    lastException = ex;
                    lastErrorMessage = ex.Message;
                    _logger.LogWarning(ex, "Provider {Provider} failed for model {Model}, trying next provider",
                                       p.Name, request.Model);
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    lastErrorMessage = ex.Message;
                    _logger.LogWarning(ex, "Provider {Provider} failed for model {Model}, trying next provider",
                                       p.Name, request.Model);
                }
            }

        // All providers failed
        stopwatch.Stop();
        _logger.LogError("All {Count} providers failed for model {Model}. Last error: {Error}",
                         allProviders.Count, request.Model,
                         lastErrorMessage ?? lastException?.Message ?? "Unknown error");

        return new ChatCompletionResponse
        {
            Model = request.Model,
            SystemFingerprint = "fp_error",
            ServiceTier = request.ServiceTier ?? "default",
            Choices = new List<ChatChoice>
            {
                new()
                {
                    Index = 0,
                    Message = new ChatMessage
                    {
                        Role = "assistant",
                        Content
                            = $"All providers failed for model '{request.Model}'. Last error: {lastErrorMessage ?? lastException?.Message ?? "Unknown error"}"
                    },
                    FinishReason = "error"
                }
            },
            Usage = new Usage()
        };
    }

    /// <summary>
    ///     Routes a streaming request through a virtual provider.
    /// </summary>
    private async Task RouteVirtualProviderStreamAsync(
        Provider virtualProvider,
        ChatCompletionRequest request,
        string apiKey,
        int estimatedTokens,
        Stream outputStream,
        Stopwatch stopwatch,
        string mode,
        string? specificModel,
        CancellationToken cancellationToken)
    {
        List<ProviderModel> models;
        string modeDescription;

        if (mode == "specific" && !string.IsNullOrEmpty(specificModel))
        {
            // Third mode: specific model with failover to same model on other providers
            models = await _providerService.GetProvidersForSpecificModelAsync(specificModel, virtualProvider.Name);
            modeDescription = $"specific model '{specificModel}'";
        }
        else if (mode == "auto")
        {
            models = await _providerService.GetAllModelsForAutoModeAsync(virtualProvider.Name);
            modeDescription = "auto (all models)";
        }
        else // auto-free (default)
        {
            models = await _providerService.GetFreeModelsForAutoModeAsync(virtualProvider.Name);
            modeDescription = "auto-free (free models only)";
        }

        if (models.Count == 0)
        {
            await WriteErrorEventAsync(outputStream, request.Model,
                                       $"No models available for {modeDescription}.",
                                       "rate_limit", cancellationToken);
            return;
        }

        var modelsByProvider = models
                              .GroupBy(m => m.Provider)
                              .OrderByDescending(g => g.Key.Priority)
                              .ToList();

        foreach (var providerGroup in modelsByProvider)
        {
            var provider = providerGroup.Key;
            var providerModels = providerGroup.ToList();

            if (provider.IsRateLimited) continue;

            // For specific mode, we only have one model per provider (the specific one)
            var modelsToTry = mode == "specific"
                ? providerModels.Where(m => m.ModelName == specificModel).ToList()
                : providerModels;

            foreach (var model in modelsToTry)
                using (LogContext.PushProperty("Provider", provider.Name))
                using (LogContext.PushProperty("ActualModel", model.ModelName))
                {
                    _logger.LogInformation("Trying streaming with {Provider}/{Model} (mode: {Mode})", provider.Name,
                                           model.ModelName, modeDescription);

                    try
                    {
                        var success = await TryProviderStreamWithModelAsync(provider, model.ModelName, request, apiKey,
                                                                            estimatedTokens, outputStream,
                                                                            cancellationToken);

                        if (success)
                        {
                            stopwatch.Stop();
                            _logger.LogInformation(
                                "Virtual provider streaming succeeded with {Provider}/{Model} (mode: {Mode})",
                                provider.Name, model.ModelName, modeDescription);
                            return;
                        }

                        // TryProviderStreamWithModelAsync now records usage on failure
                        _logger.LogDebug("Provider {Provider} failed for {Model}, trying next", provider.Name,
                                         model.ModelName);
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                        _logger.LogInformation(
                            "Streaming cancelled by client during virtual provider routing for {Provider}/{Model}",
                            provider.Name, model.ModelName);
                        throw;
                    }
                    catch (TaskCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                        _logger.LogInformation(
                            "Streaming cancelled by client during virtual provider routing for {Provider}/{Model}",
                            provider.Name, model.ModelName);
                        throw;
                    }
                    catch (ObjectDisposedException)
                    {
                        _logger.LogInformation(
                            "Stream disposed during virtual provider routing for {Provider}/{Model} (client disconnected)",
                            provider.Name, model.ModelName);
                        return;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Provider {Provider} failed for {Model}, trying next", provider.Name,
                                           model.ModelName);
                    }
                }
        }

        stopwatch.Stop();
        await WriteErrorEventAsync(outputStream, request.Model,
                                   $"All models exhausted for {modeDescription}.",
                                   "error", cancellationToken);
    }

    /// <summary>
    ///     Routes a completion request through a virtual provider (e.g., VanPatten/auto-free, VanPatten/auto) by trying models
    ///     from all providers.
    /// </summary>
    private async Task<CompletionResponse> RouteVirtualProviderAsync(
        Provider virtualProvider,
        CompletionRequest request,
        string apiKey,
        int estimatedTokens,
        Stopwatch stopwatch,
        string mode,
        string? specificModel,
        CancellationToken cancellationToken)
    {
        List<ProviderModel> models;
        string modeDescription;

        if (mode == "specific" && !string.IsNullOrEmpty(specificModel))
        {
            models = await _providerService.GetProvidersForSpecificModelAsync(specificModel, virtualProvider.Name);
            modeDescription = $"specific model '{specificModel}'";
        }
        else if (mode == "auto")
        {
            models = await _providerService.GetAllModelsForAutoModeAsync(virtualProvider.Name);
            modeDescription = "auto (all models)";
        }
        else
        {
            models = await _providerService.GetFreeModelsForAutoModeAsync(virtualProvider.Name);
            modeDescription = "auto-free (free models only)";
        }

        if (models.Count == 0)
        {
            _logger.LogWarning("No models available for virtual provider {VirtualProvider} (mode: {Mode})",
                               virtualProvider.Name, modeDescription);
            return new CompletionResponse
            {
                Model = request.Model,
                Choices = new List<CompletionChoice>
                {
                    new()
                    {
                        Index = 0, Text = $"No models available for {modeDescription}.", FinishReason = "rate_limit"
                    }
                },
                Usage = new Usage()
            };
        }

        _logger.LogInformation(
            "Virtual provider {VirtualProvider} routing (mode: {Mode}): {ModelCount} models available",
            virtualProvider.Name, modeDescription, models.Count);

        var modelsByProvider = models
                              .GroupBy(m => m.Provider)
                              .OrderByDescending(g => g.Key.Priority)
                              .ToList();

        Exception? lastException = null;
        foreach (var providerGroup in modelsByProvider)
        {
            var provider = providerGroup.Key;
            var providerModels = providerGroup.ToList();

            if (provider.IsRateLimited)
            {
                _logger.LogDebug("Skipping rate-limited provider: {ProviderName}", provider.Name);
                continue;
            }

            var modelsToTry = mode == "specific"
                ? providerModels.Where(m => m.ModelName == specificModel).ToList()
                : providerModels;

            foreach (var model in modelsToTry)
                using (LogContext.PushProperty("Provider", provider.Name))
                using (LogContext.PushProperty("ProviderId", provider.Id))
                using (LogContext.PushProperty("ActualModel", model.ModelName))
                {
                    _logger.LogInformation(
                        "Trying model {ModelName} from provider {ProviderName} (priority: {Priority})",
                        model.ModelName, provider.Name, provider.Priority);

                    CompletionResponse? result;
                    try
                    {
                        result = await TryProviderWithModelAsync(provider, model.ModelName, request, apiKey,
                                                                 estimatedTokens, cancellationToken);
                    }
                    catch (ProviderFailoverException ex)
                    {
                        lastException = ex;
                        _logger.LogWarning(
                            "Model {ModelName} from provider {ProviderName} failed, trying next (reason: {Reason})",
                            model.ModelName, provider.Name, ex.Message);
                        continue;
                    }
                    catch (Exception ex)
                    {
                        lastException = ex;
                        _logger.LogWarning(
                            "Model {ModelName} from provider {ProviderName} failed, trying next (reason: {Reason})",
                            model.ModelName, provider.Name, ex.Message);
                        continue;
                    }

                    if (result != null)
                    {
                        stopwatch.Stop();
                        result.Model = $"{virtualProvider.Name}/{mode} ({provider.Name}/{model.ModelName})";
                        _logger.LogInformation(
                            "Virtual provider {VirtualProvider} succeeded with {Provider}/{Model} (elapsed: {Elapsed}ms)",
                            virtualProvider.Name, provider.Name, model.ModelName, stopwatch.ElapsedMilliseconds);
                        return result;
                    }
                }
        }

        stopwatch.Stop();
        _logger.LogError("Virtual provider {VirtualProvider} (mode: {Mode}) failed - all {Count} providers exhausted",
                         virtualProvider.Name, modeDescription, modelsByProvider.Count);

        return new CompletionResponse
        {
            Model = request.Model,
            SystemFingerprint = "fp_error",
            Choices = new List<CompletionChoice>
            {
                new() { Index = 0, Text = $"All models exhausted for {modeDescription}.", FinishReason = "error" }
            },
            Usage = new Usage()
        };
    }

    /// <summary>
    ///     Routes a streaming completion request through a virtual provider.
    /// </summary>
    private async Task RouteVirtualProviderStreamAsync(
        Provider virtualProvider,
        CompletionRequest request,
        string apiKey,
        int estimatedTokens,
        Stream outputStream,
        Stopwatch stopwatch,
        string mode,
        string? specificModel,
        CancellationToken cancellationToken)
    {
        List<ProviderModel> models;
        string modeDescription;

        if (mode == "specific" && !string.IsNullOrEmpty(specificModel))
        {
            models = await _providerService.GetProvidersForSpecificModelAsync(specificModel, virtualProvider.Name);
            modeDescription = $"specific model '{specificModel}'";
        }
        else if (mode == "auto")
        {
            models = await _providerService.GetAllModelsForAutoModeAsync(virtualProvider.Name);
            modeDescription = "auto (all models)";
        }
        else
        {
            models = await _providerService.GetFreeModelsForAutoModeAsync(virtualProvider.Name);
            modeDescription = "auto-free (free models only)";
        }

        if (models.Count == 0)
        {
            await WriteCompletionErrorEventAsync(outputStream, request.Model,
                                                 $"No models available for {modeDescription}.",
                                                 "rate_limit", cancellationToken);
            return;
        }

        var modelsByProvider = models
                              .GroupBy(m => m.Provider)
                              .OrderByDescending(g => g.Key.Priority)
                              .ToList();

        foreach (var providerGroup in modelsByProvider)
        {
            var provider = providerGroup.Key;
            var providerModels = providerGroup.ToList();

            if (provider.IsRateLimited) continue;

            var modelsToTry = mode == "specific"
                ? providerModels.Where(m => m.ModelName == specificModel).ToList()
                : providerModels;

            foreach (var model in modelsToTry)
                using (LogContext.PushProperty("Provider", provider.Name))
                using (LogContext.PushProperty("ActualModel", model.ModelName))
                {
                    _logger.LogInformation("Trying streaming with {Provider}/{Model} (mode: {Mode})", provider.Name,
                                           model.ModelName, modeDescription);

                    try
                    {
                        var success = await TryProviderStreamWithModelAsync(provider, model.ModelName, request, apiKey,
                                                                            estimatedTokens, outputStream,
                                                                            cancellationToken);

                        if (success)
                        {
                            stopwatch.Stop();
                            _logger.LogInformation(
                                "Virtual provider streaming succeeded with {Provider}/{Model} (mode: {Mode})",
                                provider.Name, model.ModelName, modeDescription);
                            return;
                        }

                        _logger.LogDebug("Provider {Provider} failed for {Model}, trying next", provider.Name,
                                         model.ModelName);
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                        _logger.LogInformation(
                            "Streaming cancelled by client during virtual provider routing for {Provider}/{Model}",
                            provider.Name, model.ModelName);
                        throw;
                    }
                    catch (TaskCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                        _logger.LogInformation(
                            "Streaming cancelled by client during virtual provider routing for {Provider}/{Model}",
                            provider.Name, model.ModelName);
                        throw;
                    }
                    catch (ObjectDisposedException)
                    {
                        _logger.LogInformation(
                            "Stream disposed during virtual provider routing for {Provider}/{Model} (client disconnected)",
                            provider.Name, model.ModelName);
                        return;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Provider {Provider} failed for {Model}, trying next", provider.Name,
                                           model.ModelName);
                    }
                }
        }

        stopwatch.Stop();
        await WriteCompletionErrorEventAsync(outputStream, request.Model,
                                             $"All models exhausted for {modeDescription}.",
                                             "error", cancellationToken);
    }

    /// <summary>
    ///     Routes a moderation request through a virtual provider.
    /// </summary>
    private async Task<ModerationResponse> RouteVirtualProviderModerationAsync(
        Provider virtualProvider,
        ModerationRequest request,
        string apiKey,
        int estimatedTokens,
        Stopwatch stopwatch,
        string mode,
        string? specificModel,
        CancellationToken cancellationToken)
    {
        List<ProviderModel> models;
        string modeDescription;

        if (mode == "specific" && !string.IsNullOrEmpty(specificModel))
        {
            models = await _providerService.GetProvidersForSpecificModelAsync(specificModel, virtualProvider.Name);
            modeDescription = $"specific model '{specificModel}'";
        }
        else if (mode == "auto")
        {
            models = await _providerService.GetAllModelsForAutoModeAsync(virtualProvider.Name);
            modeDescription = "auto (all models)";
        }
        else // auto-free (default)
        {
            models = await _providerService.GetFreeModelsForAutoModeAsync(virtualProvider.Name);
            modeDescription = "auto-free (free models only)";
        }

        if (models.Count == 0)
        {
            _logger.LogWarning("No models available for virtual provider {VirtualProvider} (mode: {Mode})",
                               virtualProvider.Name, modeDescription);
            return new ModerationResponse
            {
                Model = request.Model,
                Results = new List<ModerationResult>
                {
                    new()
                    {
                        Flagged = false,
                        Categories = new ModerationCategories(),
                        CategoryScores = new ModerationCategoryScores()
                    }
                }
            };
        }

        _logger.LogInformation(
            "Virtual provider {VirtualProvider} routing (mode: {Mode}): {ModelCount} models available",
            virtualProvider.Name, modeDescription, models.Count);

        var modelsByProvider = models
                              .GroupBy(m => m.Provider)
                              .OrderByDescending(g => g.Key.Priority)
                              .ToList();

        Exception? lastException = null;
        string? lastErrorMessage = null;
        foreach (var providerGroup in modelsByProvider)
        {
            var provider = providerGroup.Key;
            var providerModels = providerGroup.ToList();

            if (provider.IsRateLimited)
            {
                _logger.LogDebug("Skipping rate-limited provider: {ProviderName}", provider.Name);
                continue;
            }

            var modelsToTry = mode == "specific"
                ? providerModels.Where(m => m.ModelName == specificModel).ToList()
                : providerModels;

            foreach (var model in modelsToTry)
                using (LogContext.PushProperty("Provider", provider.Name))
                using (LogContext.PushProperty("ProviderId", provider.Id))
                using (LogContext.PushProperty("ActualModel", model.ModelName))
                {
                    _logger.LogInformation(
                        "Trying model {ModelName} from provider {ProviderName} (priority: {Priority})",
                        model.ModelName, provider.Name, provider.Priority);

                    ModerationResponse? result;
                    try
                    {
                        result = await TryProviderWithModelAsync(provider, model.ModelName, request, apiKey,
                                                                 estimatedTokens, cancellationToken);
                    }
                    catch (ProviderFailoverException ex)
                    {
                        lastException = ex;
                        lastErrorMessage = ex.Message;
                        _logger.LogWarning(
                            "Model {ModelName} from provider {ProviderName} failed, trying next (reason: {Reason})",
                            model.ModelName, provider.Name, ex.Message);
                        continue;
                    }
                    catch (Exception ex)
                    {
                        lastException = ex;
                        _logger.LogWarning(
                            "Model {ModelName} from provider {ProviderName} failed, trying next (reason: {Reason})",
                            model.ModelName, provider.Name, ex.Message);
                        continue;
                    }

                    if (result != null)
                    {
                        stopwatch.Stop();
                        result.Model = $"{virtualProvider.Name}/{mode} ({provider.Name}/{model.ModelName})";
                        _logger.LogInformation(
                            "Virtual provider {VirtualProvider} succeeded with {Provider}/{Model} (elapsed: {Elapsed}ms)",
                            virtualProvider.Name, provider.Name, model.ModelName, stopwatch.ElapsedMilliseconds);
                        return result;
                    }
                }
        }

        stopwatch.Stop();
        _logger.LogError("Virtual provider {VirtualProvider} (mode: {Mode}) failed - all {Count} providers exhausted",
                         virtualProvider.Name, modeDescription, modelsByProvider.Count);

        return new ModerationResponse
        {
            Model = request.Model,
            Results = new List<ModerationResult>
            {
                new()
                {
                    Flagged = false,
                    Categories = new ModerationCategories(),
                    CategoryScores = new ModerationCategoryScores()
                }
            }
        };
    }

    /// <summary>
    ///     Attempts a non-streaming completion from a specific provider with a specific model.
    /// </summary>
    private async Task<CompletionResponse?> TryProviderWithModelAsync(
        Provider provider,
        string actualModelName,
        CompletionRequest request,
        string apiKey,
        int estimatedTokens,
        CancellationToken cancellationToken)
    {
        var retryPolicy = _retryPolicyService.GetRetryPolicy<CompletionResponse>();

        return await retryPolicy.ExecuteAsync(async ctx =>
        {
            var providerStopwatch = Stopwatch.StartNew();
            var connectionStopwatch = Stopwatch.StartNew();
            var disconnectReason = "Unknown";

            try
            {
                var client = CreateDefaultClient();
                ConfigureClientForProvider(client, provider);

                _logger.LogDebug(
                    "Initiating non-streaming request to provider {Provider} model {Model} (BaseUrl: {BaseUrl})",
                    provider.Name, actualModelName, provider.BaseUrl);

                var requestBody = BuildCompletionRequestBody(request, actualModelName);

                var json = JsonSerializer.Serialize(requestBody, new JsonSerializerOptions
                {
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });

                var content = new StringContent(json, Encoding.UTF8, "application/json");

                _logger.LogDebug("Sending non-streaming request to {Provider} ({Model}) - Request size: {Size} bytes",
                                 provider.Name, actualModelName, json.Length);

                using var requestMsg = new HttpRequestMessage(HttpMethod.Post, "completions")
                {
                    Content = content
                };

                using var response
                    = await client.SendAsync(requestMsg, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

                connectionStopwatch.Stop();

                _logger.LogDebug(
                    "HTTP response headers received from {Provider} ({Model}) - Status: {StatusCode}, Connection time: {ConnTime}ms",
                    provider.Name, actualModelName, response.StatusCode, connectionStopwatch.ElapsedMilliseconds);

                var responseString = await response.Content.ReadAsStringAsync(cancellationToken);

                providerStopwatch.Stop();

                if (!response.IsSuccessStatusCode)
                {
                    disconnectReason = $"HTTP {(int)response.StatusCode} {response.StatusCode}";
                    var errorDescription = GetErrorDescription((int)response.StatusCode);
                    _logger.LogError(
                        "Provider {Provider} returned error: {ErrorDescription} - {Response} (elapsed: {Elapsed}ms, connection: {ConnTime}ms)",
                        provider.Name, errorDescription, responseString, providerStopwatch.ElapsedMilliseconds,
                        connectionStopwatch.ElapsedMilliseconds);

                    var errorAction = GetErrorAction((int)response.StatusCode);

                    if (errorAction == ProviderErrorAction.Failover)
                    {
                        _logger.LogInformation(
                            "Provider {Provider} error {StatusCode} ({ErrorDescription}) is non-retryable, failing over. Disconnect reason: {Reason}",
                            provider.Name, response.StatusCode, errorDescription, disconnectReason);
                        throw new ProviderFailoverException(
                            $"Provider {provider.Name} returned {(int)response.StatusCode}: {responseString}");
                    }

                    _logger.LogWarning(
                        "Provider {Provider} error {StatusCode} ({ErrorDescription}) is retryable, will retry. Disconnect reason: {Reason}",
                        provider.Name, response.StatusCode, errorDescription, disconnectReason);
                    throw new HttpRequestException(
                        $"Provider {provider.Name} returned {(int)response.StatusCode}: {responseString}");
                }

                var completionResponse = JsonSerializer.Deserialize<CompletionResponse>(
                    responseString, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true,
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                    });

                if (completionResponse != null) completionResponse.Model = request.Model;

                if (completionResponse != null)
                    completionResponse.SystemFingerprint = GenerateSystemFingerprint(provider.Name, actualModelName);

                var tokensUsed = completionResponse?.Usage?.TotalTokens ?? estimatedTokens;
                await _providerService.RecordUsageAsync(provider.Id, tokensUsed);

                _logger.LogInformation(
                    "Successfully completed completion with provider {Provider} (elapsed: {Elapsed}ms, tokens: {TotalTokens})",
                    provider.Name, providerStopwatch.ElapsedMilliseconds, tokensUsed);

                return completionResponse ?? new CompletionResponse { Model = request.Model };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calling provider {Provider} for model {Model}", provider.Name,
                                 actualModelName);
                throw;
            }
        }, cancellationToken);
    }

    /// <summary>
    ///     Attempts a moderation request from a specific provider with a specific model.
    /// </summary>
    private async Task<ModerationResponse?> TryProviderWithModelAsync(
        Provider provider,
        string actualModelName,
        ModerationRequest request,
        string apiKey,
        int estimatedTokens,
        CancellationToken cancellationToken)
    {
        var retryPolicy = _retryPolicyService.GetRetryPolicy<ModerationResponse>();

        return await retryPolicy.ExecuteAsync(async ctx =>
        {
            var providerStopwatch = Stopwatch.StartNew();

            try
            {
                var client = CreateDefaultClient();
                ConfigureClientForProvider(client, provider);

                _logger.LogDebug(
                    "Initiating moderation request to provider {Provider} model {Model} (BaseUrl: {BaseUrl})",
                    provider.Name, actualModelName, provider.BaseUrl);

                var requestBody = BuildModerationRequestBody(request, actualModelName);

                var json = JsonSerializer.Serialize(requestBody, new JsonSerializerOptions
                {
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });

                var content = new StringContent(json, Encoding.UTF8, "application/json");

                using var requestMsg = new HttpRequestMessage(HttpMethod.Post, "moderations")
                {
                    Content = content
                };

                using var response
                    = await client.SendAsync(requestMsg, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

                var responseString = await response.Content.ReadAsStringAsync(cancellationToken);

                providerStopwatch.Stop();

                if (!response.IsSuccessStatusCode)
                {
                    var errorDescription = GetErrorDescription((int)response.StatusCode);
                    _logger.LogError(
                        "Provider {Provider} returned error: {ErrorDescription} - {Response} (elapsed: {Elapsed}ms)",
                        provider.Name, errorDescription, responseString, providerStopwatch.ElapsedMilliseconds);

                    var errorAction = GetErrorAction((int)response.StatusCode);

                    if (errorAction == ProviderErrorAction.Failover)
                    {
                        _logger.LogInformation(
                            "Provider {Provider} error {StatusCode} ({ErrorDescription}) is non-retryable, failing over",
                            provider.Name, response.StatusCode, errorDescription);
                        throw new ProviderFailoverException(
                            $"Provider {provider.Name} returned {(int)response.StatusCode}: {responseString}");
                    }

                    _logger.LogWarning(
                        "Provider {Provider} error {StatusCode} ({ErrorDescription}) is retryable, will retry",
                        provider.Name, response.StatusCode, errorDescription);
                    throw new HttpRequestException(
                        $"Provider {provider.Name} returned {(int)response.StatusCode}: {responseString}");
                }

                var moderationResponse = JsonSerializer.Deserialize<ModerationResponse>(
                    responseString, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true,
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                    });

                if (moderationResponse != null) moderationResponse.Model = request.Model ?? actualModelName;

                var tokensUsed = moderationResponse?.Results?.Count > 0 ? estimatedTokens : 0;
                await _providerService.RecordUsageAsync(provider.Id, tokensUsed);

                _logger.LogInformation(
                    "Successfully completed moderation with provider {Provider} (elapsed: {Elapsed}ms, tokens: {TotalTokens})",
                    provider.Name, providerStopwatch.ElapsedMilliseconds, tokensUsed);

                return moderationResponse ?? new ModerationResponse { Model = request.Model ?? actualModelName };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calling provider {Provider} for model {Model}", provider.Name,
                                 actualModelName);
                throw;
            }
        }, cancellationToken);
    }

    /// <summary>
    ///     Attempts to stream a completion from a specific provider with a specific model.
    /// </summary>
    private async Task<bool> TryProviderStreamWithModelAsync(
        Provider provider,
        string actualModelName,
        CompletionRequest request,
        string apiKey,
        int estimatedTokens,
        Stream outputStream,
        CancellationToken cancellationToken)
    {
        var providerStopwatch = Stopwatch.StartNew();
        var connectionStopwatch = Stopwatch.StartNew();
        var usageRecorded = false;
        var disconnectReason = "Unknown";

        try
        {
            var client = CreateStreamingClient();
            ConfigureClientForProvider(client, provider);

            _logger.LogInformation(
                "Initiating streaming connection to provider {Provider} model {Model} (BaseUrl: {BaseUrl})",
                provider.Name, actualModelName, provider.BaseUrl);

            var requestBodyDict = BuildCompletionRequestBody(request, actualModelName);

            requestBodyDict["stream"] = true;

            var json = JsonSerializer.Serialize(requestBodyDict, new JsonSerializerOptions
            {
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            var content = new StringContent(json, Encoding.UTF8, "application/json");

            using var requestMsg = new HttpRequestMessage(HttpMethod.Post, "completions")
            {
                Content = content
            };

            _logger.LogDebug("Sending streaming request to {Provider} ({Model}) - Request size: {Size} bytes",
                             provider.Name, actualModelName, json.Length);

            using var response
                = await client.SendAsync(requestMsg, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

            connectionStopwatch.Stop();
            providerStopwatch.Stop();

            _logger.LogInformation(
                "HTTP response headers received from {Provider} ({Model}) - Status: {StatusCode}, Connection time: {ConnTime}ms, Total time: {TotalTime}ms",
                provider.Name, actualModelName, response.StatusCode, connectionStopwatch.ElapsedMilliseconds,
                providerStopwatch.ElapsedMilliseconds);

            if (!response.IsSuccessStatusCode)
            {
                var responseString = await response.Content.ReadAsStringAsync(cancellationToken);
                disconnectReason = $"HTTP {(int)response.StatusCode} {response.StatusCode}";
                var errorDescription = GetErrorDescription((int)response.StatusCode);
                _logger.LogError(
                    "Provider {Provider} streaming error: {ErrorDescription} - {Response} (elapsed: {Elapsed}ms, connection: {ConnTime}ms)",
                    provider.Name, errorDescription, responseString, providerStopwatch.ElapsedMilliseconds,
                    connectionStopwatch.ElapsedMilliseconds);

                var errorAction = GetErrorAction((int)response.StatusCode);

                if (errorAction == ProviderErrorAction.Failover)
                {
                    _logger.LogInformation(
                        "Provider {Provider} streaming error {StatusCode} ({ErrorDescription}) is non-retryable, failing over. Disconnect reason: {Reason}",
                        provider.Name, response.StatusCode, errorDescription, disconnectReason);
                    throw new ProviderFailoverException(
                        $"Provider {provider.Name} returned {(int)response.StatusCode}: {responseString}");
                }

                _logger.LogWarning(
                    "Provider {Provider} streaming error {StatusCode} ({ErrorDescription}) is retryable, will failover. Disconnect reason: {Reason}",
                    provider.Name, response.StatusCode, errorDescription, disconnectReason);
                throw new HttpRequestException(
                    $"Provider {provider.Name} returned {(int)response.StatusCode}: {responseString}");
            }

            using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var buffer = new byte[8192];
            int bytesRead;
            var firstChunkReceived = false;

            while ((bytesRead = await responseStream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken))
                > 0)
            {
                if (!firstChunkReceived)
                {
                    firstChunkReceived = true;
                    _logger.LogDebug("First completion chunk received from {Provider} ({Model})", provider.Name,
                                     actualModelName);
                }

                await outputStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
                await outputStream.FlushAsync(cancellationToken);
            }

            if (!firstChunkReceived)
                _logger.LogWarning("Streaming completion completed with NO CHUNKS received from {Provider} ({Model})",
                                   provider.Name, actualModelName);

            if (!usageRecorded)
            {
                await _providerService.RecordUsageAsync(provider.Id, estimatedTokens);
                usageRecorded = true;
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error streaming from provider {Provider} for model {Model}", provider.Name,
                             actualModelName);
            if (!usageRecorded)
            {
                await _providerService.RecordUsageAsync(provider.Id, estimatedTokens);
                usageRecorded = true;
            }

            throw;
        }
    }

    /// <summary>
    ///     Attempts to stream a chat completion from a specific provider with a specific model.
    /// </summary>
    /// <param name="provider">The provider to stream from.</param>
    /// <param name="actualModelName">The actual model name.</param>
    /// <param name="request">The chat completion request.</param>
    /// <param name="apiKey">The API key.</param>
    /// <param name="estimatedTokens">Estimated token count.</param>
    /// <param name="outputStream">The output stream for SSE events.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if streaming succeeded, false otherwise.</returns>
    private async Task<bool> TryProviderStreamWithModelAsync(
        Provider provider,
        string actualModelName,
        ChatCompletionRequest request,
        string apiKey,
        int estimatedTokens,
        Stream outputStream,
        CancellationToken cancellationToken)
    {
        var providerStopwatch = Stopwatch.StartNew();
        var connectionStopwatch = Stopwatch.StartNew();
        var usageRecorded = false;
        var disconnectReason = "Unknown";

        try
        {
            // Use streaming client (no timeout) for streaming requests
            var client = CreateStreamingClient();
            ConfigureClientForProvider(client, provider);

            _logger.LogInformation(
                "Initiating streaming connection to provider {Provider} model {Model} (BaseUrl: {BaseUrl})",
                provider.Name, actualModelName, provider.BaseUrl);

            var requestBodyDict = BuildRequestBody(request, actualModelName);

            // Force streaming to true for streaming requests
            requestBodyDict["stream"] = true;

            var json = JsonSerializer.Serialize(requestBodyDict, new JsonSerializerOptions
            {
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            var content = new StringContent(json, Encoding.UTF8, "application/json");

            using var requestMsg = new HttpRequestMessage(HttpMethod.Post, "chat/completions")
            {
                Content = content
            };

            _logger.LogDebug("Sending streaming request to {Provider} ({Model}) - Request size: {Size} bytes",
                             provider.Name, actualModelName, json.Length);

            // Send request and measure connection establishment time
            using var response
                = await client.SendAsync(requestMsg, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

            connectionStopwatch.Stop();
            providerStopwatch.Stop();

            _logger.LogInformation(
                "HTTP response headers received from {Provider} ({Model}) - Status: {StatusCode}, Connection time: {ConnTime}ms, Total time: {TotalTime}ms",
                provider.Name, actualModelName, response.StatusCode, connectionStopwatch.ElapsedMilliseconds,
                providerStopwatch.ElapsedMilliseconds);

            if (!response.IsSuccessStatusCode)
            {
                var responseString = await response.Content.ReadAsStringAsync(cancellationToken);
                disconnectReason = $"HTTP {(int)response.StatusCode} {response.StatusCode}";
                var errorDescription = GetErrorDescription((int)response.StatusCode);
                _logger.LogError(
                    "Provider {Provider} streaming error: {ErrorDescription} - {Response} (elapsed: {Elapsed}ms, connection: {ConnTime}ms)",
                    provider.Name, errorDescription, responseString, providerStopwatch.ElapsedMilliseconds,
                    connectionStopwatch.ElapsedMilliseconds);

                var errorAction = GetErrorAction((int)response.StatusCode);

                if (errorAction == ProviderErrorAction.Failover)
                {
                    // Non-retryable error - return false to trigger failover
                    _logger.LogInformation(
                        "Provider {Provider} streaming error {StatusCode} ({ErrorDescription}) is non-retryable, failing over. Disconnect reason: {Reason}",
                        provider.Name, response.StatusCode, errorDescription, disconnectReason);

                    // Record usage for rate limiting even on failover
                    await _providerService.RecordUsageAsync(provider.Id, estimatedTokens);
                    usageRecorded = true;
                    return false;
                }

                // Retryable error - for streaming, we don't retry the stream itself,
                // but we can failover to next provider
                _logger.LogWarning(
                    "Provider {Provider} streaming error {StatusCode} ({ErrorDescription}) is retryable, failing over to next provider. Disconnect reason: {Reason}",
                    provider.Name, response.StatusCode, errorDescription, disconnectReason);

                // Record usage for rate limiting even on retryable error
                await _providerService.RecordUsageAsync(provider.Id, estimatedTokens);
                usageRecorded = true;
                return false;
            }

            _logger.LogInformation(
                "Started streaming from provider {Provider} model {Model} (connection: {Elapsed}ms, headers received: {ConnTime}ms)",
                provider.Name, actualModelName, providerStopwatch.ElapsedMilliseconds,
                connectionStopwatch.ElapsedMilliseconds);

            // Read streaming response with detailed logging
            using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var streamReadStopwatch = Stopwatch.StartNew();
            var lastChunkTime = DateTime.UtcNow;
            long totalBytes = 0;
            var chunkCount = 0;
            var estimatedCompletionTokens = 0;
            var firstChunkReceived = false;

            var buffer = new byte[8192];
            int bytesRead;

            _logger.LogDebug("Starting to read streaming response from {Provider} ({Model})", provider.Name,
                             actualModelName);

            while ((bytesRead = await responseStream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken))
                > 0)
            {
                var chunkStopwatch = Stopwatch.StartNew();
                var timeSinceLastChunk = DateTime.UtcNow - lastChunkTime;
                lastChunkTime = DateTime.UtcNow;

                if (!firstChunkReceived)
                {
                    firstChunkReceived = true;
                    _logger.LogInformation(
                        "First chunk received from {Provider} ({Model}) - Time to first byte: {TTFB}ms",
                        provider.Name, actualModelName, streamReadStopwatch.ElapsedMilliseconds);
                }

                await outputStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
                await outputStream.FlushAsync(cancellationToken);

                chunkStopwatch.Stop();
                streamReadStopwatch.Stop();
                totalBytes += bytesRead;
                chunkCount++;

                var chunkText = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                if (chunkText.Contains("\"content\""))
                    estimatedCompletionTokens += chunkText.Split("\"content\"").Length - 1;

                // Log slow chunks (>500ms) which might indicate network issues
                if (chunkStopwatch.ElapsedMilliseconds > 500)
                    _logger.LogWarning(
                        "Slow chunk read from {Provider} ({Model}): {ChunkTime}ms, chunk #{ChunkCount}, size: {Size} bytes, time since last: {SinceLast}ms",
                        provider.Name, actualModelName, chunkStopwatch.ElapsedMilliseconds, chunkCount, bytesRead,
                        timeSinceLastChunk.TotalMilliseconds);

                // Log periodic stats every 50 chunks
                if (chunkCount % 50 == 0)
                    _logger.LogDebug(
                        "Streaming progress from {Provider} ({Model}): {ChunkCount} chunks, {TotalBytes} bytes, elapsed: {Elapsed}ms",
                        provider.Name, actualModelName, chunkCount, totalBytes,
                        streamReadStopwatch.ElapsedMilliseconds);
            }

            streamReadStopwatch.Stop();

            // Handle stream_options.include_usage - send final usage chunk before [DONE]
            if (request.StreamOptions?.IncludeUsage == true && firstChunkReceived)
            {
                var usageResponse = new ChatCompletionResponse
                {
                    Id = Guid.NewGuid().ToString("N")[..24],
                    Object = "chat.completion.chunk",
                    Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                    Model = request.Model,
                    Choices = new List<ChatChoice>
                    {
                        new()
                        {
                            Index = 0,
                            Delta = new ChatMessageDelta
                            {
                                Role = "assistant",
                                Content = null
                            },
                            FinishReason = "stop"
                        }
                    },
                    Usage = new Usage
                    {
                        PromptTokens = estimatedTokens,
                        CompletionTokens = estimatedCompletionTokens,
                        TotalTokens = estimatedTokens + estimatedCompletionTokens,
                        CompletionTokensDetails = new CompletionTokensDetails
                        {
                            ReasoningTokens = 0,
                            AudioTokens = 0,
                            AcceptedPredictionTokens = 0,
                            RejectedPredictionTokens = 0
                        }
                    }
                };

                var usageJson = JsonSerializer.Serialize(usageResponse, new JsonSerializerOptions
                {
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });

                var usageBytes = Encoding.UTF8.GetBytes($"data: {usageJson}\n\n");
                await outputStream.WriteAsync(usageBytes, cancellationToken);
                await outputStream.FlushAsync(cancellationToken);

                _logger.LogDebug("Sent final usage chunk for streaming request");
            }

            // Handle stream_options.include_obfuscation - add obfuscation to final [DONE] if needed
            // Note: Full obfuscation would require modifying each chunk, which is complex.
            // For now, we log if requested but don't implement full obfuscation.
            if (request.StreamOptions?.IncludeObfuscation == true)
                _logger.LogDebug(
                    "Stream obfuscation requested but not fully implemented - chunks pass through unmodified");

            if (firstChunkReceived)
            {
                _logger.LogInformation(
                    "Streaming completed successfully from {Provider} ({Model}): {ChunkCount} chunks, {TotalBytes} bytes, streaming time: {StreamTime}ms, total time: {TotalTime}ms, avg chunk time: {AvgChunkTime:F2}ms",
                    provider.Name, actualModelName, chunkCount, totalBytes, streamReadStopwatch.ElapsedMilliseconds,
                    providerStopwatch.ElapsedMilliseconds,
                    chunkCount > 0 ? (double)streamReadStopwatch.ElapsedMilliseconds / chunkCount : 0);
            }
            else
            {
                disconnectReason = "No chunks received - empty stream";
                _logger.LogWarning(
                    "Streaming completed with NO CHUNKS received from {Provider} ({Model}) - possible premature disconnect. Total time: {TotalTime}ms",
                    provider.Name, actualModelName, providerStopwatch.ElapsedMilliseconds);
            }

            var tokensUsed = estimatedTokens + estimatedCompletionTokens;
            await _providerService.RecordUsageAsync(provider.Id, tokensUsed);
            usageRecorded = true;

            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            disconnectReason = "Client cancelled request (OperationCanceledException)";
            _logger.LogInformation(
                "Streaming cancelled by client for {Provider} ({Model}) after {Elapsed}ms. Reason: {Reason}",
                provider.Name, actualModelName, providerStopwatch.ElapsedMilliseconds, disconnectReason);
            throw;
        }
        catch (TaskCanceledException ex) when (cancellationToken.IsCancellationRequested)
        {
            disconnectReason = "Client cancelled request (TaskCanceledException)";
            _logger.LogInformation(
                "Streaming cancelled by client for {Provider} ({Model}) after {Elapsed}ms. Reason: {Reason}",
                provider.Name, actualModelName, providerStopwatch.ElapsedMilliseconds, disconnectReason);
            throw;
        }
        catch (ObjectDisposedException)
        {
            disconnectReason = "Stream disposed (client disconnected mid-stream)";
            _logger.LogInformation(
                "Stream disposed for {Provider} ({Model}) after {Elapsed}ms - client likely disconnected",
                provider.Name, actualModelName, providerStopwatch.ElapsedMilliseconds);
            if (!usageRecorded) await _providerService.RecordUsageAsync(provider.Id, estimatedTokens);
            return false;
        }
        catch (IOException ex) when (ex.HResult == -2147024894) // ERROR_INVALID_HANDLE / disposed
        {
            disconnectReason = "Stream disposed (IO error - client disconnected)";
            _logger.LogInformation(
                "Stream IO error for {Provider} ({Model}) after {Elapsed}ms - client likely disconnected (HResult=0x{HResult:X})",
                provider.Name, actualModelName, providerStopwatch.ElapsedMilliseconds, ex.HResult);
            if (!usageRecorded) await _providerService.RecordUsageAsync(provider.Id, estimatedTokens);
            return false;
        }
        catch (HttpRequestException ex) when (ex.InnerException is SocketException socketEx)
        {
            disconnectReason = $"Socket error: {socketEx.SocketErrorCode} - {socketEx.Message}";
            _logger.LogError(
                "SOCKET ERROR - Provider {Provider} model {Model} disconnected: SocketErrorCode={SocketErrorCode}, Message={Message}, NativeErrorCode={NativeErrorCode}, Elapsed={Elapsed}ms",
                provider.Name, actualModelName, socketEx.SocketErrorCode, socketEx.Message, socketEx.NativeErrorCode,
                providerStopwatch.ElapsedMilliseconds);

            if (!usageRecorded) await _providerService.RecordUsageAsync(provider.Id, estimatedTokens);
            return false;
        }
        catch (HttpRequestException ex)
        {
            disconnectReason = $"HTTP request error: {ex.Message}";
            _logger.LogError(
                "HTTP REQUEST ERROR - Provider {Provider} model {Model} failed: {Message}, StatusCode={StatusCode}, Elapsed={Elapsed}ms",
                provider.Name, actualModelName, ex.Message, ex.StatusCode, providerStopwatch.ElapsedMilliseconds);

            if (!usageRecorded) await _providerService.RecordUsageAsync(provider.Id, estimatedTokens);
            return false;
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            disconnectReason = $"Request timeout (TaskCanceledException): {ex.Message}";
            _logger.LogError(
                "TIMEOUT - Provider {Provider} model {Model} request timed out after {Elapsed}ms. InnerException: {InnerEx}",
                provider.Name, actualModelName, providerStopwatch.ElapsedMilliseconds, ex.InnerException?.Message);

            if (!usageRecorded) await _providerService.RecordUsageAsync(provider.Id, estimatedTokens);
            return false;
        }
        catch (IOException ex)
        {
            disconnectReason = $"IO error (stream broken): {ex.Message}";
            _logger.LogError(
                "IO ERROR - Provider {Provider} model {Model} stream broken: {Message}, HResult=0x{HResult:X}, Elapsed={Elapsed}ms",
                provider.Name, actualModelName, ex.Message, ex.HResult, providerStopwatch.ElapsedMilliseconds);

            if (!usageRecorded) await _providerService.RecordUsageAsync(provider.Id, estimatedTokens);
            return false;
        }
        catch (Exception ex)
        {
            disconnectReason = $"Unexpected error: {ex.GetType().Name}: {ex.Message}";
            _logger.LogError(
                "UNEXPECTED ERROR - Provider {Provider} model {Model} failed: {ExceptionType}: {Message}, Elapsed={Elapsed}ms",
                provider.Name, actualModelName, ex.GetType().Name, ex.Message, providerStopwatch.ElapsedMilliseconds);

            // Write error event to stream
            await WriteErrorEventAsync(outputStream, request.Model,
                                       $"Provider {provider.Name} failed: {ex.Message}. Trying next provider...",
                                       "error", cancellationToken);

            if (!usageRecorded) await _providerService.RecordUsageAsync(provider.Id, estimatedTokens);
            return false;
        }
    }

    /// <summary>
    ///     Routes a streaming request to a single provider WITH FAILOVER to other providers.
    /// </summary>
    private async Task RouteSingleProviderStreamAsync(
        Provider provider,
        ChatCompletionRequest request,
        string apiKey,
        int estimatedTokens,
        Stream outputStream,
        Stopwatch stopwatch,
        CancellationToken cancellationToken)
    {
        // Get ALL available providers for this model (in priority order)
        var allProviders = await _providerService.GetAllAvailableProvidersForModelAsync(request.Model, estimatedTokens);

        if (allProviders.Count == 0)
        {
            stopwatch.Stop();
            _logger.LogWarning("No available providers for model: {Model}", request.Model);
            await WriteErrorEventAsync(outputStream, request.Model,
                                       "No available provider for model. All providers are rate limited or not configured.",
                                       "rate_limit", cancellationToken);
            return;
        }

        Exception? lastException = null;
        string? lastErrorMessage = null;

        // Try each provider in priority order until one succeeds
        foreach (var p in allProviders)
            using (LogContext.PushProperty("Provider", p.Name))
            using (LogContext.PushProperty("ProviderId", p.Id))
            {
                _logger.LogInformation(
                    "Trying streaming with provider {Provider} for model {Model} (priority: {Priority})",
                    p.Name, request.Model, p.Priority);

                try
                {
                    var success = await TryProviderStreamWithModelAsync(p, request.Model, request, apiKey,
                                                                        estimatedTokens, outputStream,
                                                                        cancellationToken);

                    if (success)
                    {
                        stopwatch.Stop();
                        _logger.LogInformation("Streaming completed with provider {Provider} (elapsed: {Elapsed}ms)",
                                               p.Name, stopwatch.ElapsedMilliseconds);
                        return;
                    }

                    // Streaming failed - record usage for rate limiting even on failure
                    _logger.LogWarning("Provider {Provider} streaming failed, trying next provider", p.Name);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    _logger.LogInformation(
                        "Streaming cancelled by client during provider routing for {Provider}/{Model}",
                        p.Name, request.Model);
                    throw;
                }
                catch (TaskCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    _logger.LogInformation(
                        "Streaming cancelled by client during provider routing for {Provider}/{Model}",
                        p.Name, request.Model);
                    throw;
                }
                catch (ObjectDisposedException)
                {
                    _logger.LogInformation(
                        "Stream disposed during provider routing for {Provider}/{Model} (client disconnected)",
                        p.Name, request.Model);
                    return;
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    lastErrorMessage = ex.Message;
                    _logger.LogWarning(
                        ex, "Provider {Provider} streaming failed for model {Model}, trying next provider",
                        p.Name, request.Model);
                }
            }

        // All providers failed
        stopwatch.Stop();
        var lastErrorDesc = lastException != null
            ? $"{lastException.GetType().Name}: {lastErrorMessage}"
            : "Unknown error";
        _logger.LogError("All {Count} providers failed for streaming model {Model}. Last error: {Error}",
                         allProviders.Count, request.Model, lastErrorDesc);

        await WriteErrorEventAsync(outputStream, request.Model,
                                   $"All providers failed for model '{request.Model}'. Last error: {lastErrorDesc}",
                                   "error", cancellationToken);
    }

    /// <summary>
    ///     Routes an embedding request to a specific provider.
    /// </summary>
    private async Task<EmbeddingResponse> RouteEmbeddingToProviderAsync(
        Provider provider,
        EmbeddingRequest request,
        string apiKey,
        int estimatedTokens,
        Stopwatch stopwatch,
        CancellationToken cancellationToken)
    {
        var retryPolicy = _retryPolicyService.GetRetryPolicy<EmbeddingResponse>();

        return await retryPolicy.ExecuteAsync(async ctx =>
        {
            var providerStopwatch = Stopwatch.StartNew();

            using (LogContext.PushProperty("Provider", provider.Name))
            using (LogContext.PushProperty("ProviderId", provider.Id))
            {
                _logger.LogInformation("Routing embedding to provider {Provider} for model {Model}", provider.Name,
                                       request.Model);

                var client = CreateDefaultClient();
                client.BaseAddress = new Uri(provider.BaseUrl.TrimEnd('/') + "/");
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", provider.ApiKey);

                var headers = ParseHeaders(provider.Headers);
                foreach (var header in headers)
                    client.DefaultRequestHeaders.TryAddWithoutValidation(header.Key, header.Value);

                var providerModel = await GetProviderModelNameAsync(provider.Id, request.Model);

                var requestBody = new
                {
                    model = providerModel,
                    input = request.Input,
                    encoding_format = request.EncodingFormat,
                    dimensions = request.Dimensions,
                    user = request.User
                };

                var json = JsonSerializer.Serialize(requestBody, new JsonSerializerOptions
                {
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });

                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await client.PostAsync("embeddings", content, cancellationToken);
                var responseString = await response.Content.ReadAsStringAsync(cancellationToken);

                providerStopwatch.Stop();

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError(
                        "Provider {Provider} embedding error: {StatusCode} - {Response} (elapsed: {Elapsed}ms)",
                        provider.Name, response.StatusCode, responseString, providerStopwatch.ElapsedMilliseconds);

                    if (response.StatusCode == HttpStatusCode.TooManyRequests ||
                        response.StatusCode == (HttpStatusCode)429)
                        _logger.LogWarning("Provider {Provider} returned rate limit error (429)", provider.Name);

                    throw new HttpRequestException(
                        $"Provider {provider.Name} returned {(int)response.StatusCode}: {responseString}");
                }

                var embeddingResponse = JsonSerializer.Deserialize<EmbeddingResponse>(
                    responseString, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true,
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                    });

                if (embeddingResponse != null) embeddingResponse.Model = request.Model;

                var tokensUsed = embeddingResponse?.Usage?.TotalTokens ?? estimatedTokens;
                await _providerService.RecordUsageAsync(provider.Id, tokensUsed);

                _logger.LogInformation(
                    "Successfully completed embedding with provider {Provider} (elapsed: {Elapsed}ms, tokens: {TotalTokens})",
                    provider.Name, providerStopwatch.ElapsedMilliseconds, tokensUsed);

                return embeddingResponse ?? new EmbeddingResponse { Model = request.Model };
            }
        }, cancellationToken);
    }

    /// <summary>
    ///     Routes a completion request to a specific provider.
    /// </summary>
    private async Task<CompletionResponse> RouteCompletionToProviderAsync(
        Provider provider,
        CompletionRequest request,
        string apiKey,
        int estimatedTokens,
        Stopwatch stopwatch,
        CancellationToken cancellationToken)
    {
        var retryPolicy = _retryPolicyService.GetRetryPolicy<CompletionResponse>();

        return await retryPolicy.ExecuteAsync(async ctx =>
        {
            var providerStopwatch = Stopwatch.StartNew();

            using (LogContext.PushProperty("Provider", provider.Name))
            using (LogContext.PushProperty("ProviderId", provider.Id))
            {
                _logger.LogInformation("Routing completion to provider {Provider} for model {Model}", provider.Name,
                                       request.Model);

                var client = CreateDefaultClient();
                client.BaseAddress = new Uri(provider.BaseUrl.TrimEnd('/') + "/");
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", provider.ApiKey);

                var headers = ParseHeaders(provider.Headers);
                foreach (var header in headers)
                    client.DefaultRequestHeaders.TryAddWithoutValidation(header.Key, header.Value);

                var providerModel = await GetProviderModelNameAsync(provider.Id, request.Model);

                var requestBody = BuildCompletionRequestBody(request, providerModel);

                var json = JsonSerializer.Serialize(requestBody, new JsonSerializerOptions
                {
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });

                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await client.PostAsync("completions", content, cancellationToken);
                var responseString = await response.Content.ReadAsStringAsync(cancellationToken);

                providerStopwatch.Stop();

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError(
                        "Provider {Provider} completion error: {StatusCode} - {Response} (elapsed: {Elapsed}ms)",
                        provider.Name, response.StatusCode, responseString, providerStopwatch.ElapsedMilliseconds);

                    if (response.StatusCode == HttpStatusCode.TooManyRequests ||
                        response.StatusCode == (HttpStatusCode)429)
                        _logger.LogWarning("Provider {Provider} returned rate limit error (429)", provider.Name);

                    throw new HttpRequestException(
                        $"Provider {provider.Name} returned {(int)response.StatusCode}: {responseString}");
                }

                var completionResponse = JsonSerializer.Deserialize<CompletionResponse>(
                    responseString, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true,
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                    });

                if (completionResponse != null) completionResponse.Model = request.Model;

                // Add OpenAI-compatible fields
                if (completionResponse != null)
                    completionResponse.SystemFingerprint = GenerateSystemFingerprint(provider.Name, providerModel);

                var tokensUsed = completionResponse?.Usage?.TotalTokens ?? estimatedTokens;
                await _providerService.RecordUsageAsync(provider.Id, tokensUsed);

                _logger.LogInformation(
                    "Successfully completed completion with provider {Provider} (elapsed: {Elapsed}ms, tokens: {TotalTokens})",
                    provider.Name, providerStopwatch.ElapsedMilliseconds, tokensUsed);

                return completionResponse ?? new CompletionResponse { Model = request.Model };
            }
        }, cancellationToken);
    }

    /// <summary>
    ///     Routes a streaming completion request to a specific provider.
    /// </summary>
    private async Task RouteCompletionStreamToProviderAsync(
        Provider provider,
        CompletionRequest request,
        string apiKey,
        int estimatedTokens,
        Stream outputStream,
        CancellationToken cancellationToken)
    {
        var client = CreateStreamingClient();
        ConfigureClientForProvider(client, provider);

        _logger.LogInformation(
            "Initiating streaming completion connection to provider {Provider} model {Model} (BaseUrl: {BaseUrl})",
            provider.Name, request.Model, provider.BaseUrl);

        var requestBodyDict
            = BuildCompletionRequestBody(request, await GetProviderModelNameAsync(provider.Id, request.Model));
        requestBodyDict["stream"] = true;

        var json = JsonSerializer.Serialize(requestBodyDict, new JsonSerializerOptions
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        var content = new StringContent(json, Encoding.UTF8, "application/json");

        using var requestMsg = new HttpRequestMessage(HttpMethod.Post, "completions")
        {
            Content = content
        };

        _logger.LogDebug("Sending streaming completion request to {Provider} ({Model}) - Request size: {Size} bytes",
                         provider.Name, request.Model, json.Length);

        var connectionStopwatch = Stopwatch.StartNew();
        using var response
            = await client.SendAsync(requestMsg, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        connectionStopwatch.Stop();

        var providerStopwatch = Stopwatch.StartNew();

        _logger.LogInformation(
            "HTTP response headers received from {Provider} ({Model}) - Status: {StatusCode}, Connection time: {ConnTime}ms",
            provider.Name, request.Model, response.StatusCode, connectionStopwatch.ElapsedMilliseconds);

        if (!response.IsSuccessStatusCode)
        {
            var responseString = await response.Content.ReadAsStringAsync(cancellationToken);
            var errorDescription = GetErrorDescription((int)response.StatusCode);
            _logger.LogError(
                "Provider {Provider} streaming completion error: {ErrorDescription} - {Response} (connection: {ConnTime}ms)",
                provider.Name, errorDescription, responseString, connectionStopwatch.ElapsedMilliseconds);

            throw new HttpRequestException(
                $"Provider {provider.Name} returned {(int)response.StatusCode}: {responseString}");
        }

        _logger.LogInformation(
            "Started streaming completion from provider {Provider} model {Model} (connection: {Elapsed}ms)",
            provider.Name, request.Model, connectionStopwatch.ElapsedMilliseconds);

        // Read streaming response
        using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var buffer = new byte[8192];
        int bytesRead;
        var firstChunkReceived = false;

        while ((bytesRead = await responseStream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken)) > 0)
        {
            if (!firstChunkReceived)
            {
                firstChunkReceived = true;
                _logger.LogDebug("First completion chunk received from {Provider} ({Model})", provider.Name,
                                 request.Model);
            }

            await outputStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
            await outputStream.FlushAsync(cancellationToken);
        }

        if (!firstChunkReceived)
            _logger.LogWarning("Streaming completion completed with NO CHUNKS received from {Provider} ({Model})",
                               provider.Name, request.Model);
    }

    /// <summary>
    ///     Builds the request body for a completion request using passthrough serialization.
    /// </summary>
    private static Dictionary<string, object?> BuildCompletionRequestBody(CompletionRequest request, string modelName)
    {
        var options = new JsonSerializerOptions
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        // Serialize the entire request to JSON, then deserialize to a dictionary for manipulation
        var json = JsonSerializer.Serialize(request, options);
        var dict = JsonSerializer.Deserialize<Dictionary<string, object?>>(json, options)
            ?? new Dictionary<string, object?>();

        // Override model with the actual model name being sent to the provider
        dict["model"] = modelName;

        // Handle prompt - can be string or array of strings
        if (request.Prompt != null) dict["prompt"] = request.Prompt;

        return dict;
    }

    /// <summary>
    ///     Estimates token count from a prompt (string or array of strings).
    /// </summary>
    private static int EstimateTokensFromPrompt(object prompt)
    {
        if (prompt == null) return 0;
        if (prompt is string s) return s.Length / 4;
        if (prompt is JsonElement el && el.ValueKind == JsonValueKind.Array)
        {
            var total = 0;
            foreach (var item in el.EnumerateArray())
                if (item.ValueKind == JsonValueKind.String)
                    total += item.GetString()?.Length ?? 0;
            return total / 4;
        }

        return prompt.ToString()?.Length / 4 ?? 0;
    }

    /// <summary>
    ///     Writes an error event to the SSE output stream for completions.
    /// </summary>
    private async Task WriteCompletionErrorEventAsync(
        Stream outputStream,
        string model,
        string message,
        string finishReason,
        CancellationToken cancellationToken)
    {
        try
        {
            var errorResponse = new CompletionResponse
            {
                Model = model,
                Choices = new List<CompletionChoice>
                {
                    new()
                    {
                        Index = 0,
                        Text = message,
                        FinishReason = finishReason
                    }
                },
                Usage = new Usage()
            };

            var errorJson = JsonSerializer.Serialize(errorResponse, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            var errorBytes = Encoding.UTF8.GetBytes($"data: {errorJson}\n\ndata: [DONE]\n\n");
            await outputStream.WriteAsync(errorBytes, cancellationToken);
            await outputStream.FlushAsync(cancellationToken);
        }
        catch (ObjectDisposedException)
        {
            // Stream was disposed (client disconnected) - silently ignore
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Client cancelled - expected
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to write completion error event to output stream");
        }
    }

    /// <summary>
    ///     Builds the request body for a chat completion request using passthrough serialization.
    ///     This automatically includes all parameters from the request, only overriding model and stream.
    /// </summary>
    private static Dictionary<string, object?> BuildRequestBody(ChatCompletionRequest request, string modelName)
    {
        var options = new JsonSerializerOptions
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        // Serialize the entire request to JSON, then deserialize to a dictionary for manipulation
        var json = JsonSerializer.Serialize(request, options);
        var dict = JsonSerializer.Deserialize<Dictionary<string, object?>>(json, options)
            ?? new Dictionary<string, object?>();

        // Override model with the actual model name being sent to the provider
        dict["model"] = modelName;

        // For non-streaming requests, ensure stream is false (or remove if not needed)
        // For streaming requests, the caller will override this anyway
        // We keep the original stream value from the request

        // Handle messages serialization - they need special handling for tool_calls
        if (request.Messages != null && request.Messages.Count > 0)
            dict["messages"] = request.Messages.Select(m => new
            {
                role = m.Role,
                content = m.Content,
                name = m.Name,
                tool_calls = m.ToolCalls?.Select(tc => new
                {
                    id = tc.Id,
                    type = tc.Type,
                    function = new { name = tc.Function.Name, arguments = tc.Function.Arguments }
                }),
                tool_call_id = m.ToolCallId
            }).ToList();

        // Handle tools serialization - ensure proper serialization
        if (request.Tools != null && request.Tools.Count > 0) dict["tools"] = request.Tools;

        // Handle response_format - ensure proper serialization
        if (request.ResponseFormat != null) dict["response_format"] = request.ResponseFormat;

        return dict;
    }

    /// <summary>
    ///     Writes an error event to the SSE output stream.
    /// </summary>
    private async Task WriteErrorEventAsync(
        Stream outputStream,
        string model,
        string message,
        string finishReason,
        CancellationToken cancellationToken)
    {
        try
        {
            var errorResponse = new ChatCompletionResponse
            {
                Model = model,
                Choices = new List<ChatChoice>
                {
                    new()
                    {
                        Index = 0,
                        Message = new ChatMessage
                        {
                            Role = "assistant",
                            Content = message
                        },
                        FinishReason = finishReason
                    }
                },
                Usage = new Usage()
            };

            var errorJson = JsonSerializer.Serialize(errorResponse, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            var errorBytes = Encoding.UTF8.GetBytes($"data: {errorJson}\n\ndata: [DONE]\n\n");
            await outputStream.WriteAsync(errorBytes, cancellationToken);
            await outputStream.FlushAsync(cancellationToken);
        }
        catch (ObjectDisposedException)
        {
            // Stream was disposed (client disconnected) - silently ignore
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Client cancelled - expected
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to write error event to output stream (client may have disconnected)");
        }
    }

    /// <summary>
    ///     Estimates token count from a list of chat messages.
    /// </summary>
    private static int EstimateTokens(List<ChatMessage> messages)
    {
        var totalChars = messages.Sum(m => m.GetTextContent().Length);
        return totalChars / 4; // Rough estimate: 4 chars per token
    }

    /// <summary>
    ///     Estimates token count from a list of input strings.
    /// </summary>
    private static int EstimateTokens(List<string> inputs)
    {
        var totalChars = inputs.Sum(s => s?.Length ?? 0);
        return totalChars / 4;
    }

    /// <summary>
    ///     Gets the provider-specific model name for a given model.
    /// </summary>
    private async Task<string> GetProviderModelNameAsync(int providerId, string modelName)
    {
        var providerModel = await _providerService.GetProviderByIdAsync(providerId);
        var model = providerModel?.Models.FirstOrDefault(m => m.ModelName == modelName);
        return model?.ProviderModelName ?? modelName;
    }

    /// <summary>
    ///     Parses headers from JSON string or key:value format.
    /// </summary>
    private static Dictionary<string, string> ParseHeaders(string? headersJson)
    {
        if (string.IsNullOrWhiteSpace(headersJson)) return new Dictionary<string, string>();

        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, string>>(headersJson)
                ?? new Dictionary<string, string>();
        }
        catch
        {
            var dict = new Dictionary<string, string>();
            var parts = headersJson.Split(',', StringSplitOptions.RemoveEmptyEntries);
            foreach (var part in parts)
            {
                var kv = part.Split(':', 2);
                if (kv.Length == 2) dict[kv[0].Trim()] = kv[1].Trim();
            }

            return dict;
        }
    }

    /// <summary>
    ///     Generates a deterministic system fingerprint based on provider and model.
    /// </summary>
    internal static string GenerateSystemFingerprint(string providerName, string modelName)
    {
        var input = $"{providerName}:{modelName}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        // Return first 12 chars of hex (like OpenAI's fp_ prefix + 12 chars)
        return "fp_" + Convert.ToHexString(hash)[..12].ToLowerInvariant();
    }

    /// <summary>
    ///     Routes a moderation request to a specific provider.
    /// </summary>
    private async Task<ModerationResponse> RouteModerationToProviderAsync(
        Provider provider,
        ModerationRequest request,
        string apiKey,
        int estimatedTokens,
        Stopwatch stopwatch,
        CancellationToken cancellationToken)
    {
        var retryPolicy = _retryPolicyService.GetRetryPolicy<ModerationResponse>();

        return await retryPolicy.ExecuteAsync(async ctx =>
        {
            var providerStopwatch = Stopwatch.StartNew();

            using (LogContext.PushProperty("Provider", provider.Name))
            using (LogContext.PushProperty("ProviderId", provider.Id))
            {
                _logger.LogInformation("Routing moderation to provider {Provider} for model {Model}", provider.Name,
                                       request.Model ?? "default");

                var client = CreateDefaultClient();
                client.BaseAddress = new Uri(provider.BaseUrl.TrimEnd('/') + "/");
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", provider.ApiKey);

                var headers = ParseHeaders(provider.Headers);
                foreach (var header in headers)
                    client.DefaultRequestHeaders.TryAddWithoutValidation(header.Key, header.Value);

                var providerModel
                    = await GetProviderModelNameAsync(provider.Id, request.Model ?? "omni-moderation-latest");

                var requestBody = BuildModerationRequestBody(request, providerModel);

                var json = JsonSerializer.Serialize(requestBody, new JsonSerializerOptions
                {
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });

                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await client.PostAsync("moderations", content, cancellationToken);
                var responseString = await response.Content.ReadAsStringAsync(cancellationToken);

                providerStopwatch.Stop();

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError(
                        "Provider {Provider} moderation error: {StatusCode} - {Response} (elapsed: {Elapsed}ms)",
                        provider.Name, response.StatusCode, responseString, providerStopwatch.ElapsedMilliseconds);

                    if (response.StatusCode == HttpStatusCode.TooManyRequests ||
                        response.StatusCode == (HttpStatusCode)429)
                        _logger.LogWarning("Provider {Provider} returned rate limit error (429)", provider.Name);

                    throw new HttpRequestException(
                        $"Provider {provider.Name} returned {(int)response.StatusCode}: {responseString}");
                }

                var moderationResponse = JsonSerializer.Deserialize<ModerationResponse>(
                    responseString, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true,
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                    });

                if (moderationResponse != null) moderationResponse.Model = request.Model ?? providerModel;

                var tokensUsed = moderationResponse?.Results?.Count > 0 ? estimatedTokens : 0;
                await _providerService.RecordUsageAsync(provider.Id, tokensUsed);

                _logger.LogInformation(
                    "Successfully completed moderation with provider {Provider} (elapsed: {Elapsed}ms, tokens: {TotalTokens})",
                    provider.Name, providerStopwatch.ElapsedMilliseconds, tokensUsed);

                return moderationResponse ?? new ModerationResponse { Model = request.Model ?? providerModel };
            }
        }, cancellationToken);
    }

    /// <summary>
    ///     Routes an image generation request to a specific provider.
    /// </summary>
    private async Task<ImageGenerationResponse> RouteImageGenerationToProviderAsync(
        Provider provider,
        ImageGenerationRequest request,
        string apiKey,
        Stopwatch stopwatch,
        CancellationToken cancellationToken)
    {
        var retryPolicy = _retryPolicyService.GetRetryPolicy<ImageGenerationResponse>();

        return await retryPolicy.ExecuteAsync(async ctx =>
        {
            var providerStopwatch = Stopwatch.StartNew();

            using (LogContext.PushProperty("Provider", provider.Name))
            using (LogContext.PushProperty("ProviderId", provider.Id))
            {
                _logger.LogInformation("Routing image generation to provider {Provider} for model {Model}",
                                       provider.Name,
                                       request.Model ?? "dall-e-3");

                var client = CreateDefaultClient();
                client.BaseAddress = new Uri(provider.BaseUrl.TrimEnd('/') + "/");
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", provider.ApiKey);

                var headers = ParseHeaders(provider.Headers);
                foreach (var header in headers)
                    client.DefaultRequestHeaders.TryAddWithoutValidation(header.Key, header.Value);

                var providerModel = await GetProviderModelNameAsync(provider.Id, request.Model ?? "dall-e-3");

                var requestBody = BuildImageGenerationRequestBody(request, providerModel);

                var json = JsonSerializer.Serialize(requestBody, new JsonSerializerOptions
                {
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });

                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await client.PostAsync("images/generations", content, cancellationToken);
                var responseString = await response.Content.ReadAsStringAsync(cancellationToken);

                providerStopwatch.Stop();

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError(
                        "Provider {Provider} image generation error: {StatusCode} - {Response} (elapsed: {Elapsed}ms)",
                        provider.Name, response.StatusCode, responseString, providerStopwatch.ElapsedMilliseconds);

                    if (response.StatusCode == HttpStatusCode.TooManyRequests ||
                        response.StatusCode == (HttpStatusCode)429)
                        _logger.LogWarning("Provider {Provider} returned rate limit error (429)", provider.Name);

                    throw new HttpRequestException(
                        $"Provider {provider.Name} returned {(int)response.StatusCode}: {responseString}");
                }

                var imageResponse = JsonSerializer.Deserialize<ImageGenerationResponse>(
                    responseString, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true,
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                    });

                if (imageResponse != null) imageResponse.Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

                _logger.LogInformation(
                    "Successfully completed image generation with provider {Provider} (elapsed: {Elapsed}ms)",
                    provider.Name, providerStopwatch.ElapsedMilliseconds);

                return imageResponse ?? new ImageGenerationResponse
                    { Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds() };
            }
        }, cancellationToken);
    }

    /// <summary>
    ///     Routes an image edit request to a specific provider.
    /// </summary>
    private async Task<ImageGenerationResponse> RouteImageEditToProviderAsync(
        Provider provider,
        ImageEditRequest request,
        string apiKey,
        Stopwatch stopwatch,
        CancellationToken cancellationToken)
    {
        var retryPolicy = _retryPolicyService.GetRetryPolicy<ImageGenerationResponse>();

        return await retryPolicy.ExecuteAsync(async ctx =>
        {
            var providerStopwatch = Stopwatch.StartNew();

            using (LogContext.PushProperty("Provider", provider.Name))
            using (LogContext.PushProperty("ProviderId", provider.Id))
            {
                _logger.LogInformation("Routing image edit to provider {Provider} for model {Model}", provider.Name,
                                       request.Model ?? "dall-e-2");

                var client = CreateDefaultClient();
                client.BaseAddress = new Uri(provider.BaseUrl.TrimEnd('/') + "/");
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", provider.ApiKey);

                var headers = ParseHeaders(provider.Headers);
                foreach (var header in headers)
                    client.DefaultRequestHeaders.TryAddWithoutValidation(header.Key, header.Value);

                var providerModel = await GetProviderModelNameAsync(provider.Id, request.Model ?? "dall-e-2");

                // For multipart/form-data (images/edits)
                var requestContent = new MultipartFormDataContent();
                requestContent.Add(new ByteArrayContent(request.Image, 0, request.Image.Length), "image", "image.png");
                requestContent.Add(new StringContent(request.Prompt), "prompt");
                requestContent.Add(new StringContent(providerModel), "model");

                if (request.Mask != null)
                    requestContent.Add(new ByteArrayContent(request.Mask, 0, request.Mask.Length), "mask", "mask.png");
                if (request.N.HasValue) requestContent.Add(new StringContent(request.N.Value.ToString()), "n");
                if (!string.IsNullOrEmpty(request.Size)) requestContent.Add(new StringContent(request.Size), "size");
                if (!string.IsNullOrEmpty(request.ResponseFormat))
                    requestContent.Add(new StringContent(request.ResponseFormat), "response_format");
                if (!string.IsNullOrEmpty(request.User)) requestContent.Add(new StringContent(request.User), "user");

                var response = await client.PostAsync("images/edits", requestContent, cancellationToken);
                var responseString = await response.Content.ReadAsStringAsync(cancellationToken);

                providerStopwatch.Stop();

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError(
                        "Provider {Provider} image edit error: {StatusCode} - {Response} (elapsed: {Elapsed}ms)",
                        provider.Name, response.StatusCode, responseString, providerStopwatch.ElapsedMilliseconds);

                    if (response.StatusCode == HttpStatusCode.TooManyRequests ||
                        response.StatusCode == (HttpStatusCode)429)
                        _logger.LogWarning("Provider {Provider} returned rate limit error (429)", provider.Name);

                    throw new HttpRequestException(
                        $"Provider {provider.Name} returned {(int)response.StatusCode}: {responseString}");
                }

                var imageResponse = JsonSerializer.Deserialize<ImageGenerationResponse>(
                    responseString, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true,
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                    });

                if (imageResponse != null) imageResponse.Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

                _logger.LogInformation(
                    "Successfully completed image edit with provider {Provider} (elapsed: {Elapsed}ms)",
                    provider.Name, providerStopwatch.ElapsedMilliseconds);

                return imageResponse ?? new ImageGenerationResponse
                    { Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds() };
            }
        }, cancellationToken);
    }

    /// <summary>
    ///     Routes an image variation request to a specific provider.
    /// </summary>
    private async Task<ImageGenerationResponse> RouteImageVariationToProviderAsync(
        Provider provider,
        ImageVariationRequest request,
        string apiKey,
        Stopwatch stopwatch,
        CancellationToken cancellationToken)
    {
        var retryPolicy = _retryPolicyService.GetRetryPolicy<ImageGenerationResponse>();

        return await retryPolicy.ExecuteAsync(async ctx =>
        {
            var providerStopwatch = Stopwatch.StartNew();

            using (LogContext.PushProperty("Provider", provider.Name))
            using (LogContext.PushProperty("ProviderId", provider.Id))
            {
                _logger.LogInformation("Routing image variation to provider {Provider} for model {Model}",
                                       provider.Name,
                                       request.Model ?? "dall-e-2");

                var client = CreateDefaultClient();
                client.BaseAddress = new Uri(provider.BaseUrl.TrimEnd('/') + "/");
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", provider.ApiKey);

                var headers = ParseHeaders(provider.Headers);
                foreach (var header in headers)
                    client.DefaultRequestHeaders.TryAddWithoutValidation(header.Key, header.Value);

                var providerModel = await GetProviderModelNameAsync(provider.Id, request.Model ?? "dall-e-2");

                // For multipart/form-data (images/variations)
                var requestContent = new MultipartFormDataContent();
                requestContent.Add(new ByteArrayContent(request.Image, 0, request.Image.Length), "image", "image.png");
                requestContent.Add(new StringContent(providerModel), "model");

                if (request.N.HasValue) requestContent.Add(new StringContent(request.N.Value.ToString()), "n");
                if (!string.IsNullOrEmpty(request.Size)) requestContent.Add(new StringContent(request.Size), "size");
                if (!string.IsNullOrEmpty(request.ResponseFormat))
                    requestContent.Add(new StringContent(request.ResponseFormat), "response_format");
                if (!string.IsNullOrEmpty(request.User)) requestContent.Add(new StringContent(request.User), "user");

                var response = await client.PostAsync("images/variations", requestContent, cancellationToken);
                var responseString = await response.Content.ReadAsStringAsync(cancellationToken);

                providerStopwatch.Stop();

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError(
                        "Provider {Provider} image variation error: {StatusCode} - {Response} (elapsed: {Elapsed}ms)",
                        provider.Name, response.StatusCode, responseString, providerStopwatch.ElapsedMilliseconds);

                    if (response.StatusCode == HttpStatusCode.TooManyRequests ||
                        response.StatusCode == (HttpStatusCode)429)
                        _logger.LogWarning("Provider {Provider} returned rate limit error (429)", provider.Name);

                    throw new HttpRequestException(
                        $"Provider {provider.Name} returned {(int)response.StatusCode}: {responseString}");
                }

                var imageResponse = JsonSerializer.Deserialize<ImageGenerationResponse>(
                    responseString, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true,
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                    });

                if (imageResponse != null) imageResponse.Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

                _logger.LogInformation(
                    "Successfully completed image variation with provider {Provider} (elapsed: {Elapsed}ms)",
                    provider.Name, providerStopwatch.ElapsedMilliseconds);

                return imageResponse ?? new ImageGenerationResponse
                    { Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds() };
            }
        }, cancellationToken);
    }

    /// <summary>
    ///     Routes an audio transcription request to a specific provider.
    /// </summary>
    private async Task<AudioTranscriptionResponse> RouteAudioTranscriptionToProviderAsync(
        Provider provider,
        AudioTranscriptionRequest request,
        string apiKey,
        Stopwatch stopwatch,
        CancellationToken cancellationToken)
    {
        var retryPolicy = _retryPolicyService.GetRetryPolicy<AudioTranscriptionResponse>();

        return await retryPolicy.ExecuteAsync(async ctx =>
        {
            var providerStopwatch = Stopwatch.StartNew();

            using (LogContext.PushProperty("Provider", provider.Name))
            using (LogContext.PushProperty("ProviderId", provider.Id))
            {
                _logger.LogInformation("Routing audio transcription to provider {Provider} for model {Model}",
                                       provider.Name,
                                       request.Model ?? "whisper-1");

                var client = CreateDefaultClient();
                client.BaseAddress = new Uri(provider.BaseUrl.TrimEnd('/') + "/");
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", provider.ApiKey);

                var headers = ParseHeaders(provider.Headers);
                foreach (var header in headers)
                    client.DefaultRequestHeaders.TryAddWithoutValidation(header.Key, header.Value);

                var providerModel = await GetProviderModelNameAsync(provider.Id, request.Model ?? "whisper-1");

                // For multipart/form-data (audio/transcriptions)
                var requestContent = new MultipartFormDataContent();
                requestContent.Add(new ByteArrayContent(request.File, 0, request.File.Length), "file",
                                   request.FileName);
                requestContent.Add(new StringContent(providerModel), "model");

                if (!string.IsNullOrEmpty(request.Language))
                    requestContent.Add(new StringContent(request.Language), "language");
                if (!string.IsNullOrEmpty(request.Prompt))
                    requestContent.Add(new StringContent(request.Prompt), "prompt");
                if (!string.IsNullOrEmpty(request.ResponseFormat))
                    requestContent.Add(new StringContent(request.ResponseFormat), "response_format");
                if (request.Temperature.HasValue)
                    requestContent.Add(new StringContent(request.Temperature.Value.ToString()), "temperature");
                if (request.TimestampGranularities != null && request.TimestampGranularities.Count > 0)
                    requestContent.Add(new StringContent(string.Join(",", request.TimestampGranularities)),
                                       "timestamp_granularities");

                var response = await client.PostAsync("audio/transcriptions", requestContent, cancellationToken);
                var responseString = await response.Content.ReadAsStringAsync(cancellationToken);

                providerStopwatch.Stop();

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError(
                        "Provider {Provider} audio transcription error: {StatusCode} - {Response} (elapsed: {Elapsed}ms)",
                        provider.Name, response.StatusCode, responseString, providerStopwatch.ElapsedMilliseconds);

                    if (response.StatusCode == HttpStatusCode.TooManyRequests ||
                        response.StatusCode == (HttpStatusCode)429)
                        _logger.LogWarning("Provider {Provider} returned rate limit error (429)", provider.Name);

                    throw new HttpRequestException(
                        $"Provider {provider.Name} returned {(int)response.StatusCode}: {responseString}");
                }

                var audioResponse = JsonSerializer.Deserialize<AudioTranscriptionResponse>(
                    responseString, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true,
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                    });

                _logger.LogInformation(
                    "Successfully completed audio transcription with provider {Provider} (elapsed: {Elapsed}ms)",
                    provider.Name, providerStopwatch.ElapsedMilliseconds);

                return audioResponse ?? new AudioTranscriptionResponse { Text = "Transcription failed" };
            }
        }, cancellationToken);
    }

    /// <summary>
    ///     Routes an audio translation request to a specific provider.
    /// </summary>
    private async Task<AudioTranscriptionResponse> RouteAudioTranslationToProviderAsync(
        Provider provider,
        AudioTranslationRequest request,
        string apiKey,
        Stopwatch stopwatch,
        CancellationToken cancellationToken)
    {
        var retryPolicy = _retryPolicyService.GetRetryPolicy<AudioTranscriptionResponse>();

        return await retryPolicy.ExecuteAsync(async ctx =>
        {
            var providerStopwatch = Stopwatch.StartNew();

            using (LogContext.PushProperty("Provider", provider.Name))
            using (LogContext.PushProperty("ProviderId", provider.Id))
            {
                _logger.LogInformation("Routing audio translation to provider {Provider} for model {Model}",
                                       provider.Name,
                                       request.Model ?? "whisper-1");

                var client = CreateDefaultClient();
                client.BaseAddress = new Uri(provider.BaseUrl.TrimEnd('/') + "/");
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", provider.ApiKey);

                var headers = ParseHeaders(provider.Headers);
                foreach (var header in headers)
                    client.DefaultRequestHeaders.TryAddWithoutValidation(header.Key, header.Value);

                var providerModel = await GetProviderModelNameAsync(provider.Id, request.Model ?? "whisper-1");

                // For multipart/form-data (audio/translations)
                var requestContent = new MultipartFormDataContent();
                requestContent.Add(new ByteArrayContent(request.File, 0, request.File.Length), "file",
                                   request.FileName);
                requestContent.Add(new StringContent(providerModel), "model");

                if (!string.IsNullOrEmpty(request.Prompt))
                    requestContent.Add(new StringContent(request.Prompt), "prompt");
                if (!string.IsNullOrEmpty(request.ResponseFormat))
                    requestContent.Add(new StringContent(request.ResponseFormat), "response_format");
                if (request.Temperature.HasValue)
                    requestContent.Add(new StringContent(request.Temperature.Value.ToString()), "temperature");

                var response = await client.PostAsync("audio/translations", requestContent, cancellationToken);
                var responseString = await response.Content.ReadAsStringAsync(cancellationToken);

                providerStopwatch.Stop();

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError(
                        "Provider {Provider} audio translation error: {StatusCode} - {Response} (elapsed: {Elapsed}ms)",
                        provider.Name, response.StatusCode, responseString, providerStopwatch.ElapsedMilliseconds);

                    if (response.StatusCode == HttpStatusCode.TooManyRequests ||
                        response.StatusCode == (HttpStatusCode)429)
                        _logger.LogWarning("Provider {Provider} returned rate limit error (429)", provider.Name);

                    throw new HttpRequestException(
                        $"Provider {provider.Name} returned {(int)response.StatusCode}: {responseString}");
                }

                var audioResponse = JsonSerializer.Deserialize<AudioTranscriptionResponse>(
                    responseString, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true,
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                    });

                _logger.LogInformation(
                    "Successfully completed audio translation with provider {Provider} (elapsed: {Elapsed}ms)",
                    provider.Name, providerStopwatch.ElapsedMilliseconds);

                return audioResponse ?? new AudioTranscriptionResponse { Text = "Translation failed" };
            }
        }, cancellationToken);
    }

    /// <summary>
    ///     Routes an audio speech request to a specific provider.
    /// </summary>
    private async Task<byte[]> RouteAudioSpeechToProviderAsync(
        Provider provider,
        AudioSpeechRequest request,
        string apiKey,
        Stopwatch stopwatch,
        CancellationToken cancellationToken)
    {
        var retryPolicy = _retryPolicyService.GetRetryPolicy<byte[]>();

        return await retryPolicy.ExecuteAsync(async ctx =>
        {
            var providerStopwatch = Stopwatch.StartNew();

            using (LogContext.PushProperty("Provider", provider.Name))
            using (LogContext.PushProperty("ProviderId", provider.Id))
            {
                _logger.LogInformation("Routing audio speech to provider {Provider} for model {Model}", provider.Name,
                                       request.Model ?? "tts-1");

                var client = CreateDefaultClient();
                client.BaseAddress = new Uri(provider.BaseUrl.TrimEnd('/') + "/");
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", provider.ApiKey);

                var headers = ParseHeaders(provider.Headers);
                foreach (var header in headers)
                    client.DefaultRequestHeaders.TryAddWithoutValidation(header.Key, header.Value);

                var providerModel = await GetProviderModelNameAsync(provider.Id, request.Model ?? "tts-1");

                var requestBody = BuildAudioSpeechRequestBody(request, providerModel);

                var json = JsonSerializer.Serialize(requestBody, new JsonSerializerOptions
                {
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });

                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await client.PostAsync("audio/speech", content, cancellationToken);

                providerStopwatch.Stop();

                if (!response.IsSuccessStatusCode)
                {
                    var responseString = await response.Content.ReadAsStringAsync(cancellationToken);
                    _logger.LogError(
                        "Provider {Provider} audio speech error: {StatusCode} - {Response} (elapsed: {Elapsed}ms)",
                        provider.Name, response.StatusCode, responseString, providerStopwatch.ElapsedMilliseconds);

                    if (response.StatusCode == HttpStatusCode.TooManyRequests ||
                        response.StatusCode == (HttpStatusCode)429)
                        _logger.LogWarning("Provider {Provider} returned rate limit error (429)", provider.Name);

                    throw new HttpRequestException(
                        $"Provider {provider.Name} returned {(int)response.StatusCode}: {responseString}");
                }

                var audioBytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);

                _logger.LogInformation(
                    "Successfully completed audio speech with provider {Provider} (elapsed: {Elapsed}ms, size: {Size} bytes)",
                    provider.Name, providerStopwatch.ElapsedMilliseconds, audioBytes.Length);

                return audioBytes;
            }
        }, cancellationToken);
    }

    /// <summary>
    ///     Builds the request body for an audio speech request using passthrough serialization.
    /// </summary>
    private static Dictionary<string, object?> BuildAudioSpeechRequestBody(AudioSpeechRequest request, string modelName)
    {
        var options = new JsonSerializerOptions
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        var json = JsonSerializer.Serialize(request, options);
        var dict = JsonSerializer.Deserialize<Dictionary<string, object?>>(json, options)
            ?? new Dictionary<string, object?>();

        dict["model"] = modelName;

        return dict;
    }

    /// <summary>
    ///     Builds the request body for an image generation request using passthrough serialization.
    /// </summary>
    private static Dictionary<string, object?> BuildImageGenerationRequestBody(
        ImageGenerationRequest request, string modelName)
    {
        var options = new JsonSerializerOptions
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        // Serialize the entire request to JSON, then deserialize to a dictionary for manipulation
        var json = JsonSerializer.Serialize(request, options);
        var dict = JsonSerializer.Deserialize<Dictionary<string, object?>>(json, options)
            ?? new Dictionary<string, object?>();

        // Override model with the actual model name being sent to the provider
        dict["model"] = modelName;

        return dict;
    }

    /// <summary>
    ///     Builds the request body for a moderation request using passthrough serialization.
    /// </summary>
    private static Dictionary<string, object?> BuildModerationRequestBody(ModerationRequest request, string modelName)
    {
        var options = new JsonSerializerOptions
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        // Serialize the entire request to JSON, then deserialize to a dictionary for manipulation
        var json = JsonSerializer.Serialize(request, options);
        var dict = JsonSerializer.Deserialize<Dictionary<string, object?>>(json, options)
            ?? new Dictionary<string, object?>();

        // Override model with the actual model name being sent to the provider
        dict["model"] = modelName;

        // Handle input - can be string or array of strings
        if (request.Input != null) dict["input"] = request.Input;

        return dict;
    }

    /// <summary>
    ///     Estimates token count from moderation input (string or array of strings).
    /// </summary>
    private static int EstimateTokensFromModerationInput(object input)
    {
        if (input == null) return 0;
        if (input is string s) return s.Length / 4;
        if (input is JsonElement el && el.ValueKind == JsonValueKind.Array)
        {
            var total = 0;
            foreach (var item in el.EnumerateArray())
                if (item.ValueKind == JsonValueKind.String)
                    total += item.GetString()?.Length ?? 0;
            return total / 4;
        }

        return input.ToString()?.Length / 4 ?? 0;
    }

    /// <summary>
    ///     Determines the action to take for a provider error based on HTTP status code.
    /// </summary>
    private enum ProviderErrorAction
    {
        Failover, // Non-retryable, try next provider
        Retry     // Retryable, trigger Polly retry
    }
}