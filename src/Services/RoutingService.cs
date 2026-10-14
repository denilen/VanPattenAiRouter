using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
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
    public ProviderFailoverException(string message) : base(message)
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
}

/// <summary>
///     Service for routing AI requests to providers with failover, rate limiting, and virtual provider support.
/// </summary>
public class RoutingService : IRoutingService
{
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
    public RoutingService(
        IProviderService providerService,
        IHttpClientFactory httpClientFactory,
        RetryPolicyService retryPolicyService,
        ILogger<RoutingService> logger)
    {
        _providerService = providerService;
        _httpClientFactory = httpClientFactory;
        _retryPolicyService = retryPolicyService;
        _logger = logger;
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
            var (provider, mode, specificModel) = await _providerService.ResolveAutoProviderAsync(request.Model, estimatedTokens);

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
                return await RouteVirtualProviderAsync(provider, request, apiKey, estimatedTokens, stopwatch, mode, specificModel, default);

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
                var (provider, mode, specificModel) = await _providerService.ResolveAutoProviderAsync(request.Model, estimatedTokens);

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
                _logger.LogInformation("Streaming stream disposed for model {Model} after {Elapsed}ms (client likely disconnected)",
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

            var (provider, mode, specificModel) = await _providerService.ResolveAutoProviderAsync(request.Model, estimatedTokens);

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
                    "Virtual provider not supported for embeddings, falling back to first available provider");
                var providers
                    = await _providerService.GetAllAvailableProvidersForModelAsync(request.Model, estimatedTokens);
                provider = providers.FirstOrDefault();

                if (provider == null)
                    return new EmbeddingResponse
                    {
                        Model = request.Model,
                        Data = new List<EmbeddingData>
                            { new() { Object = "error", Index = 0, Embedding = new List<float>() } },
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
        foreach (var header in headers)
            client.DefaultRequestHeaders.TryAddWithoutValidation(header.Key, header.Value);
    }

/// <summary>
    ///     Routes a request through a virtual provider (e.g., VanPatten/auto-free, VanPatten/auto) by trying models from all providers.
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
            _logger.LogWarning("No models available for virtual provider {VirtualProvider} (mode: {Mode})", virtualProvider.Name, modeDescription);
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

        _logger.LogInformation("Virtual provider {VirtualProvider} routing (mode: {Mode}): {ModelCount} models available",
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
                        _logger.LogWarning("Model {ModelName} from provider {ProviderName} failed, trying next (reason: {Reason})",
                                           model.ModelName, provider.Name, ex.Message);
                        continue;
                    }
                    catch (Exception ex)
                    {
                        lastException = ex;
                        _logger.LogWarning("Model {ModelName} from provider {ProviderName} failed, trying next (reason: {Reason})",
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
            string disconnectReason = "Unknown";

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

                _logger.LogDebug(
                    "Sending non-streaming request to {Provider} ({Model}) - Request size: {Size} bytes",
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
                        provider.Name, errorDescription, responseString, providerStopwatch.ElapsedMilliseconds, connectionStopwatch.ElapsedMilliseconds);

                    var errorAction = GetErrorAction((int)response.StatusCode);

                    if (errorAction == ProviderErrorAction.Failover)
                    {
                        // Non-retryable error - throw failover exception with error message
                        _logger.LogInformation(
                            "Provider {Provider} error {StatusCode} ({ErrorDescription}) is non-retryable, failing over. Disconnect reason: {Reason}",
                            provider.Name, response.StatusCode, errorDescription, disconnectReason);
                        throw new ProviderFailoverException($"Provider {provider.Name} returned {(int)response.StatusCode}: {responseString}");
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

                // Record usage
                var tokensUsed = completionResponse?.Usage?.TotalTokens ?? estimatedTokens;
                await _providerService.RecordUsageAsync(provider.Id, tokensUsed);

                _logger.LogInformation(
                    "Successfully completed request with provider {Provider} model {Model} (elapsed: {Elapsed}ms, connection: {ConnTime}ms, tokens: {TotalTokens})",
                    provider.Name, actualModelName, providerStopwatch.ElapsedMilliseconds, connectionStopwatch.ElapsedMilliseconds, tokensUsed);

                return completionResponse ?? new ChatCompletionResponse { Model = request.Model };
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                _logger.LogInformation("Request cancelled for model {Model} after {Elapsed}ms", request.Model, providerStopwatch.ElapsedMilliseconds);
                throw;
            }
            catch (ProviderFailoverException)
            {
                throw; // Don't wrap failover exceptions
            }
            catch (HttpRequestException ex) when (ex.InnerException is System.Net.Sockets.SocketException socketEx)
            {
                disconnectReason = $"Socket error: {socketEx.SocketErrorCode} - {socketEx.Message}";
                _logger.LogError(ex,
                    "SOCKET ERROR - Provider {Provider} model {Model} failed: SocketErrorCode={SocketErrorCode}, Message={Message}, NativeErrorCode={NativeErrorCode}, Elapsed={Elapsed}ms",
                    provider.Name, actualModelName, socketEx.SocketErrorCode, socketEx.Message, socketEx.NativeErrorCode, providerStopwatch.ElapsedMilliseconds);
                throw;
            }
            catch (HttpRequestException ex)
            {
                disconnectReason = $"HTTP request error: {ex.Message}";
                _logger.LogError(ex,
                    "HTTP REQUEST ERROR - Provider {Provider} model {Model} failed: {Message}, StatusCode={StatusCode}, Elapsed={Elapsed}ms",
                    provider.Name, actualModelName, ex.Message, ex.StatusCode, providerStopwatch.ElapsedMilliseconds);
                throw;
            }
            catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
            {
                disconnectReason = $"Request timeout (TaskCanceledException): {ex.Message}";
                _logger.LogError(ex,
                    "TIMEOUT - Provider {Provider} model {Model} request timed out after {Elapsed}ms. InnerException: {InnerEx}",
                    provider.Name, actualModelName, providerStopwatch.ElapsedMilliseconds, ex.InnerException?.Message);
                throw;
            }
            catch (IOException ex)
            {
                disconnectReason = $"IO error: {ex.Message}";
                _logger.LogError(ex,
                    "IO ERROR - Provider {Provider} model {Model} failed: {Message}, HResult=0x{HResult:X}, Elapsed={Elapsed}ms",
                    provider.Name, actualModelName, ex.Message, ex.HResult, providerStopwatch.ElapsedMilliseconds);
                throw;
            }
            catch (Exception ex)
            {
                disconnectReason = $"Unexpected error: {ex.GetType().Name}: {ex.Message}";
                _logger.LogError(ex,
                    "UNEXPECTED ERROR - Provider {Provider} model {Model} failed: {ExceptionType}: {Message}, Elapsed={Elapsed}ms",
                    provider.Name, actualModelName, ex.GetType().Name, ex.Message, providerStopwatch.ElapsedMilliseconds);
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
                    var result = await TryProviderWithModelAsync(
                        p, request.Model, request, apiKey,
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
                    _logger.LogInformation("Trying streaming with {Provider}/{Model} (mode: {Mode})", provider.Name, model.ModelName, modeDescription);

                    try
                    {
                        var success = await TryProviderStreamWithModelAsync(provider, model.ModelName, request, apiKey,
                                                                                estimatedTokens, outputStream,
                                                                                cancellationToken);

                        if (success)
                        {
                            stopwatch.Stop();
                            _logger.LogInformation("Virtual provider streaming succeeded with {Provider}/{Model} (mode: {Mode})",
                                                   provider.Name, model.ModelName, modeDescription);
                            return;
                        }
                        
                        // TryProviderStreamWithModelAsync now records usage on failure
                        _logger.LogDebug("Provider {Provider} failed for {Model}, trying next", provider.Name, model.ModelName);
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                        _logger.LogInformation("Streaming cancelled by client during virtual provider routing for {Provider}/{Model}",
                                               provider.Name, model.ModelName);
                        throw;
                    }
                    catch (TaskCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                        _logger.LogInformation("Streaming cancelled by client during virtual provider routing for {Provider}/{Model}",
                                               provider.Name, model.ModelName);
                        throw;
                    }
                    catch (ObjectDisposedException)
                    {
                        _logger.LogInformation("Stream disposed during virtual provider routing for {Provider}/{Model} (client disconnected)",
                                               provider.Name, model.ModelName);
                        return;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Provider {Provider} failed for {Model}, trying next", provider.Name, model.ModelName);
                    }
                }
        }

        stopwatch.Stop();
        await WriteErrorEventAsync(outputStream, request.Model,
                                   $"All models exhausted for {modeDescription}.",
                                   "error", cancellationToken);
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
        bool usageRecorded = false;
        string disconnectReason = "Unknown";

        try
        {
            // Use streaming client (no timeout) for streaming requests
            var client = CreateStreamingClient();
            ConfigureClientForProvider(client, provider);

            _logger.LogInformation(
                "Initiating streaming connection to provider {Provider} model {Model} (BaseUrl: {BaseUrl})",
                provider.Name, actualModelName, provider.BaseUrl);

            var requestBody = BuildRequestBody(request, actualModelName);
            requestBody = new
            {
                model = actualModelName,
                messages = requestBody.GetType().GetProperty("messages")?.GetValue(requestBody),
                temperature = requestBody.GetType().GetProperty("temperature")?.GetValue(requestBody),
                max_tokens = requestBody.GetType().GetProperty("max_tokens")?.GetValue(requestBody),
                top_p = requestBody.GetType().GetProperty("top_p")?.GetValue(requestBody),
                n = requestBody.GetType().GetProperty("n")?.GetValue(requestBody),
                stream = true, // Force streaming
                stop = requestBody.GetType().GetProperty("stop")?.GetValue(requestBody),
                response_format = requestBody.GetType().GetProperty("response_format")?.GetValue(requestBody),
                tools = requestBody.GetType().GetProperty("tools")?.GetValue(requestBody),
                tool_choice = requestBody.GetType().GetProperty("tool_choice")?.GetValue(requestBody),
                logprobs = requestBody.GetType().GetProperty("logprobs")?.GetValue(requestBody),
                top_logprobs = requestBody.GetType().GetProperty("top_logprobs")?.GetValue(requestBody),
                user = requestBody.GetType().GetProperty("user")?.GetValue(requestBody)
            };

            var json = JsonSerializer.Serialize(requestBody, new JsonSerializerOptions
            {
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            var content = new StringContent(json, Encoding.UTF8, "application/json");

            using var requestMsg = new HttpRequestMessage(HttpMethod.Post, "chat/completions")
            {
                Content = content
            };

            _logger.LogDebug(
                "Sending streaming request to {Provider} ({Model}) - Request size: {Size} bytes",
                provider.Name, actualModelName, json.Length);

            // Send request and measure connection establishment time
            using var response
                = await client.SendAsync(requestMsg, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

            connectionStopwatch.Stop();
            providerStopwatch.Stop();

            _logger.LogInformation(
                "HTTP response headers received from {Provider} ({Model}) - Status: {StatusCode}, Connection time: {ConnTime}ms, Total time: {TotalTime}ms",
                provider.Name, actualModelName, response.StatusCode, connectionStopwatch.ElapsedMilliseconds, providerStopwatch.ElapsedMilliseconds);

if (!response.IsSuccessStatusCode)
        {
            var responseString = await response.Content.ReadAsStringAsync(cancellationToken);
            disconnectReason = $"HTTP {(int)response.StatusCode} {response.StatusCode}";
            var errorDescription = GetErrorDescription((int)response.StatusCode);
            _logger.LogError(
                "Provider {Provider} streaming error: {ErrorDescription} - {Response} (elapsed: {Elapsed}ms, connection: {ConnTime}ms)",
                provider.Name, errorDescription, responseString, providerStopwatch.ElapsedMilliseconds, connectionStopwatch.ElapsedMilliseconds);

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
                provider.Name, actualModelName, providerStopwatch.ElapsedMilliseconds, connectionStopwatch.ElapsedMilliseconds);

            // Read streaming response with detailed logging
            using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var streamReadStopwatch = Stopwatch.StartNew();
            var lastChunkTime = DateTime.UtcNow;
            long totalBytes = 0;
            int chunkCount = 0;
            var estimatedCompletionTokens = 0;
            bool firstChunkReceived = false;

            var buffer = new byte[8192];
            int bytesRead;

            _logger.LogDebug("Starting to read streaming response from {Provider} ({Model})", provider.Name, actualModelName);

            while ((bytesRead = await responseStream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken)) > 0)
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
                {
                    _logger.LogWarning(
                        "Slow chunk read from {Provider} ({Model}): {ChunkTime}ms, chunk #{ChunkCount}, size: {Size} bytes, time since last: {SinceLast}ms",
                        provider.Name, actualModelName, chunkStopwatch.ElapsedMilliseconds, chunkCount, bytesRead, timeSinceLastChunk.TotalMilliseconds);
                }

                // Log periodic stats every 50 chunks
                if (chunkCount % 50 == 0)
                {
                    _logger.LogDebug(
                        "Streaming progress from {Provider} ({Model}): {ChunkCount} chunks, {TotalBytes} bytes, elapsed: {Elapsed}ms",
                        provider.Name, actualModelName, chunkCount, totalBytes, streamReadStopwatch.ElapsedMilliseconds);
                }
            }

            streamReadStopwatch.Stop();
            
            if (firstChunkReceived)
            {
                _logger.LogInformation(
                    "Streaming completed successfully from {Provider} ({Model}): {ChunkCount} chunks, {TotalBytes} bytes, streaming time: {StreamTime}ms, total time: {TotalTime}ms, avg chunk time: {AvgChunkTime:F2}ms",
                    provider.Name, actualModelName, chunkCount, totalBytes, streamReadStopwatch.ElapsedMilliseconds, providerStopwatch.ElapsedMilliseconds,
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
            if (!usageRecorded)
            {
                await _providerService.RecordUsageAsync(provider.Id, estimatedTokens);
            }
            return false;
        }
        catch (IOException ex) when (ex.HResult == -2147024894) // ERROR_INVALID_HANDLE / disposed
        {
            disconnectReason = "Stream disposed (IO error - client disconnected)";
            _logger.LogInformation(
                "Stream IO error for {Provider} ({Model}) after {Elapsed}ms - client likely disconnected (HResult=0x{HResult:X})",
                provider.Name, actualModelName, providerStopwatch.ElapsedMilliseconds, ex.HResult);
            if (!usageRecorded)
            {
                await _providerService.RecordUsageAsync(provider.Id, estimatedTokens);
            }
            return false;
        }
        catch (HttpRequestException ex) when (ex.InnerException is System.Net.Sockets.SocketException socketEx)
        {
            disconnectReason = $"Socket error: {socketEx.SocketErrorCode} - {socketEx.Message}";
            _logger.LogError(
                "SOCKET ERROR - Provider {Provider} model {Model} disconnected: SocketErrorCode={SocketErrorCode}, Message={Message}, NativeErrorCode={NativeErrorCode}, Elapsed={Elapsed}ms",
                provider.Name, actualModelName, socketEx.SocketErrorCode, socketEx.Message, socketEx.NativeErrorCode, providerStopwatch.ElapsedMilliseconds);
            
            if (!usageRecorded)
            {
                await _providerService.RecordUsageAsync(provider.Id, estimatedTokens);
            }
            return false;
        }
        catch (HttpRequestException ex)
        {
            disconnectReason = $"HTTP request error: {ex.Message}";
            _logger.LogError(
                "HTTP REQUEST ERROR - Provider {Provider} model {Model} failed: {Message}, StatusCode={StatusCode}, Elapsed={Elapsed}ms",
                provider.Name, actualModelName, ex.Message, ex.StatusCode, providerStopwatch.ElapsedMilliseconds);
            
            if (!usageRecorded)
            {
                await _providerService.RecordUsageAsync(provider.Id, estimatedTokens);
            }
            return false;
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            disconnectReason = $"Request timeout (TaskCanceledException): {ex.Message}";
            _logger.LogError(
                "TIMEOUT - Provider {Provider} model {Model} request timed out after {Elapsed}ms. InnerException: {InnerEx}",
                provider.Name, actualModelName, providerStopwatch.ElapsedMilliseconds, ex.InnerException?.Message);
            
            if (!usageRecorded)
            {
                await _providerService.RecordUsageAsync(provider.Id, estimatedTokens);
            }
            return false;
        }
        catch (IOException ex)
        {
            disconnectReason = $"IO error (stream broken): {ex.Message}";
            _logger.LogError(
                "IO ERROR - Provider {Provider} model {Model} stream broken: {Message}, HResult=0x{HResult:X}, Elapsed={Elapsed}ms",
                provider.Name, actualModelName, ex.Message, ex.HResult, providerStopwatch.ElapsedMilliseconds);
            
            if (!usageRecorded)
            {
                await _providerService.RecordUsageAsync(provider.Id, estimatedTokens);
            }
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
            
            if (!usageRecorded)
            {
                await _providerService.RecordUsageAsync(provider.Id, estimatedTokens);
            }
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
                _logger.LogInformation("Trying streaming with provider {Provider} for model {Model} (priority: {Priority})",
                                       p.Name, request.Model, p.Priority);

                try
                {
                    var success = await TryProviderStreamWithModelAsync(p, request.Model, request, apiKey,
                                                                        estimatedTokens, outputStream, cancellationToken);

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
                    _logger.LogInformation("Streaming cancelled by client during provider routing for {Provider}/{Model}",
                                           p.Name, request.Model);
                    throw;
                }
                catch (TaskCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    _logger.LogInformation("Streaming cancelled by client during provider routing for {Provider}/{Model}",
                                           p.Name, request.Model);
                    throw;
                }
                catch (ObjectDisposedException)
                {
                    _logger.LogInformation("Stream disposed during provider routing for {Provider}/{Model} (client disconnected)",
                                           p.Name, request.Model);
                    return;
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    lastErrorMessage = ex.Message;
                    _logger.LogWarning(ex, "Provider {Provider} streaming failed for model {Model}, trying next provider",
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
    ///     Builds the request body for a chat completion request.
    /// </summary>
    private static object BuildRequestBody(ChatCompletionRequest request, string modelName)
    {
        return new
        {
            model = modelName,
            messages = request.Messages.Select(m => new
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
            }),
            temperature = request.Temperature,
            max_tokens = request.MaxTokens,
            top_p = request.TopP,
            n = request.N,
            stream = request.Stream,
            stop = request.Stop,
            response_format = request.ResponseFormat,
            tools = request.Tools,
            tool_choice = request.ToolChoice,
            logprobs = request.Logprobs,
            top_logprobs = request.TopLogprobs,
            user = request.User
        };
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
    ///     Determines the action to take for a provider error based on HTTP status code.
    /// </summary>
    private enum ProviderErrorAction
    {
        Failover, // Non-retryable, try next provider
        Retry     // Retryable, trigger Polly retry
    }
}