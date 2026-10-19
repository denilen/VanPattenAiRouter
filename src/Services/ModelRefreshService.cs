using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using VanPattenAIRouter.Data;
using VanPattenAIRouter.Models;

namespace VanPattenAIRouter.Services;

/// <summary>
///     Background service that periodically refreshes model lists from all active non-virtual providers.
/// </summary>
public class ModelRefreshService : BackgroundService
{
    private static readonly SemaphoreSlim _refreshLock = new(1, 1);
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<ModelRefreshService> _logger;
    private readonly TimeSpan _refreshInterval;
    private readonly RouterOptions _routerOptions;
    private readonly IServiceProvider _serviceProvider;

    /// <summary>
    ///     Initializes a new instance of the <see cref="ModelRefreshService" /> class.
    /// </summary>
    /// <param name="serviceProvider">The service provider for creating scoped services.</param>
    /// <param name="httpClientFactory">The HTTP client factory for making API requests.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="routerOptions">The router configuration options.</param>
    public ModelRefreshService(
        IServiceProvider serviceProvider,
        IHttpClientFactory httpClientFactory,
        ILogger<ModelRefreshService> logger,
        IOptions<RouterOptions> routerOptions)
    {
        _serviceProvider = serviceProvider;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _routerOptions = routerOptions.Value;

        _refreshInterval = TimeSpan.FromMinutes(_routerOptions.ModelRefreshIntervalMinutes);
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ModelRefreshService started with interval: {Interval} minutes",
                               _refreshInterval.TotalMinutes);

        // Initial refresh on startup
        await RefreshAllProvidersAsync(stoppingToken);

        // Periodic refresh
        while (!stoppingToken.IsCancellationRequested)
            try
            {
                await Task.Delay(_refreshInterval, stoppingToken);

                if (!stoppingToken.IsCancellationRequested) await RefreshAllProvidersAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in ModelRefreshService periodic refresh");
            }
    }

    /// <summary>
    ///     Refreshes models for all active non-virtual providers.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    private async Task RefreshAllProvidersAsync(CancellationToken cancellationToken)
    {
        // Use semaphore to prevent concurrent refresh operations
        await _refreshLock.WaitAsync(cancellationToken);
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AiRouterDbContext>();

            var providers = await context.Providers
                                         .Where(p => p.IsActive && !p.IsVirtual && !string.IsNullOrWhiteSpace(p.ApiKey))
                                         .OrderByDescending(p => p.Priority)
                                         .ToListAsync(cancellationToken);

            _logger.LogInformation("Refreshing models for {Count} active non-virtual providers", providers.Count);

            foreach (var provider in providers)
                try
                {
                    await RefreshProviderModelsAsync(provider, context, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to refresh models for provider {ProviderName}", provider.Name);
                }

            await context.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Model refresh completed for all providers");
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    /// <summary>
    ///     Refreshes the model list for a specific provider.
    /// </summary>
    /// <param name="provider">The provider to refresh.</param>
    /// <param name="context">The database context.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    private async Task RefreshProviderModelsAsync(Provider provider, AiRouterDbContext context,
                                                  CancellationToken cancellationToken)
    {
        // Skip virtual providers - they don't have real HTTP endpoints
        if (provider.IsVirtual)
        {
            _logger.LogDebug("Skipping virtual provider: {ProviderName}", provider.Name);
            return;
        }

        _logger.LogDebug("Refreshing models for provider: {ProviderName}", provider.Name);

        var client = _httpClientFactory.CreateClient();
        client.BaseAddress = new Uri(provider.BaseUrl.TrimEnd('/') + "/");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", provider.ApiKey);

        var headers = ParseHeaders(provider.Headers);
        foreach (var header in headers) client.DefaultRequestHeaders.TryAddWithoutValidation(header.Key, header.Value);

        try
        {
            var response = await client.GetAsync("models", cancellationToken);
            var responseString = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Provider {ProviderName} returned error fetching models: {StatusCode} - {Response}",
                                   provider.Name, response.StatusCode, responseString);
                return;
            }

            ModelsListResponse? modelsResponse;
            try
            {
                modelsResponse = JsonSerializer.Deserialize<ModelsListResponse>(
                    responseString, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true,
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                    });
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Provider {ProviderName} returned invalid JSON for models: {Response}",
                                 provider.Name, responseString);
                return;
            }

            if (modelsResponse?.Data == null || modelsResponse.Data.Count == 0)
            {
                _logger.LogWarning("Provider {ProviderName} returned empty models list", provider.Name);
                return;
            }

            var newModels = modelsResponse.Data
                                          .Where(m => !string.IsNullOrEmpty(m.Id))
                                          .Select(m => new ProviderModel
                                           {
                                               ModelName = m.Id,
                                               ProviderModelName = m.Id,
                                               IsEnabled = true,
                                               IsFree = IsFreeModel(m.Id, provider.Name)
                                           })
                                          .ToList();

            // Update existing models and add new ones
            var existingModels = await context.ProviderModels
                                              .Where(m => m.ProviderId == provider.Id)
                                              .ToListAsync(cancellationToken);

            var existingModelNames = existingModels.Select(m => m.ModelName).ToHashSet();
            var newModelNames = newModels.Select(m => m.ModelName).ToHashSet();

            // SOFT DELETE: Disable models that no longer exist instead of removing them
            // This prevents interrupting active streaming requests
            var modelsToDisable = existingModels.Where(m => !newModelNames.Contains(m.ModelName) && m.IsEnabled)
                                                .ToList();
            if (modelsToDisable.Count > 0)
                foreach (var model in modelsToDisable)
                {
                    model.IsEnabled = false;
                    _logger.LogDebug("Disabled obsolete model {ModelName} from provider {ProviderName}",
                                     model.ModelName, provider.Name);
                }

            // Add new models
            var modelsToAdd = newModels.Where(m => !existingModelNames.Contains(m.ModelName)).ToList();
            foreach (var model in modelsToAdd)
            {
                model.ProviderId = provider.Id;
                context.ProviderModels.Add(model);
            }

            // Update IsFree for existing models
            foreach (var existingModel in existingModels)
            {
                var newModel = newModels.FirstOrDefault(m => m.ModelName == existingModel.ModelName);
                if (newModel != null) existingModel.IsFree = newModel.IsFree;
            }

            if (modelsToAdd.Count > 0)
                _logger.LogInformation("Added {Count} new models to provider {ProviderName}: {Models}",
                                       modelsToAdd.Count, provider.Name,
                                       string.Join(", ", modelsToAdd.Select(m => m.ModelName)));

            await context.SaveChangesAsync(cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "HTTP error fetching models from provider {ProviderName}", provider.Name);
        }
        catch (TaskCanceledException)
        {
            _logger.LogWarning("Timeout fetching models from provider {ProviderName}", provider.Name);
        }
    }

    /// <summary>
    ///     Determines if a model is free based on its name and provider.
    /// </summary>
    /// <param name="modelName">The model name.</param>
    /// <param name="providerName">The provider name.</param>
    /// <returns>True if the model is considered free, false otherwise.</returns>
    private bool IsFreeModel(string modelName, string providerName)
    {
        var lowerName = modelName.ToLowerInvariant();

        // Models with "free" in name are typically free
        if (lowerName.Contains("free")) return true;

        // Provider-specific free models (hardcoded fallbacks)
        if (providerName.Equals("Local Ollama", StringComparison.OrdinalIgnoreCase))
            return true; // Local models are free

        // Load free models from configuration (Router:FreeModels) - provider prefix -> list of model patterns
        var freeModelsConfig = _routerOptions.FreeModels;
        if (freeModelsConfig != null)
            foreach (var kvp in freeModelsConfig)
            {
                var providerPrefix = kvp.Key;
                var freeModelPatterns = kvp.Value;

                if (providerName.StartsWith(providerPrefix, StringComparison.OrdinalIgnoreCase))
                    if (freeModelPatterns.Any(f => lowerName.Contains(f.ToLowerInvariant())))
                        return true;
            }

        // Generic patterns for free models across providers
        var freePatterns = new[]
        {
            ":free",
            "-free",
            "/free",
            "free-",
            "kilo-auto",
            "auto/free"
        };

        if (freePatterns.Any(p => lowerName.Contains(p))) return true;

        return false;
    }

    /// <summary>
    ///     Parses headers from JSON string or key:value format.
    /// </summary>
    /// <param name="headersJson">The headers as JSON string or comma-separated key:value pairs.</param>
    /// <returns>A dictionary of headers.</returns>
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
    ///     Internal class for deserializing the models list response from providers.
    /// </summary>
    private class ModelsListResponse
    {
        /// <summary>
        ///     Gets or sets the list of models.
        /// </summary>
        public List<ModelInfo> Data { get; set; } = new();

        /// <summary>
        ///     Gets or sets the object type.
        /// </summary>
        public string Object { get; set; } = "";
    }

    /// <summary>
    ///     Internal class for deserializing model information from provider responses.
    /// </summary>
    private class ModelInfo
    {
        /// <summary>
        ///     Gets or sets the model identifier.
        /// </summary>
        public string Id { get; } = "";

        /// <summary>
        ///     Gets or sets the object type.
        /// </summary>
        public string Object { get; set; } = "";

        /// <summary>
        ///     Gets or sets the creation timestamp.
        /// </summary>
        public long Created { get; set; }

        /// <summary>
        ///     Gets or sets the owner of the model.
        /// </summary>
        public string OwnedBy { get; set; } = "";
    }
}