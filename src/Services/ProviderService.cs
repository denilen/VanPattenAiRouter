using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using VanPattenAIRouter.Data;
using VanPattenAIRouter.Models;

namespace VanPattenAIRouter.Services;

/// <summary>
///     Interface for provider management services.
/// </summary>
public interface IProviderService
{
    /// <summary>
    ///     Gets all providers ordered by priority (highest first).
    /// </summary>
    /// <returns>A list of all providers with their models and usage statistics.</returns>
    Task<List<ProviderDto>> GetAllProvidersAsync();

    /// <summary>
    ///     Gets a provider by its ID.
    /// </summary>
    /// <param name="id">The provider ID.</param>
    /// <returns>The provider DTO, or null if not found.</returns>
    Task<ProviderDto?> GetProviderByIdAsync(int id);

    /// <summary>
    ///     Creates a new provider with the specified configuration.
    /// </summary>
    /// <param name="providerDto">The provider configuration.</param>
    /// <returns>The created provider DTO.</returns>
    Task<ProviderDto> CreateProviderAsync(ProviderDto providerDto);

    /// <summary>
    ///     Updates an existing provider.
    /// </summary>
    /// <param name="id">The provider ID.</param>
    /// <param name="providerDto">The updated provider configuration.</param>
    /// <returns>The updated provider DTO, or null if not found.</returns>
    Task<ProviderDto?> UpdateProviderAsync(int id, ProviderDto providerDto);

    /// <summary>
    ///     Deletes a provider by its ID.
    /// </summary>
    /// <param name="id">The provider ID.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task DeleteProviderAsync(int id);

    /// <summary>
    ///     Gets all available models from active, non-rate-limited providers.
    /// </summary>
    /// <returns>A list of distinct available models.</returns>
    Task<List<ModelInfo>> GetAvailableModelsAsync();

    /// <summary>
    ///     Gets available models grouped by free/paid and by provider.
    /// </summary>
    /// <returns>A grouped models response.</returns>
    Task<ModelsGroupedResponse> GetGroupedModelsAsync();

    /// <summary>
    ///     Finds a provider that offers the specified model (ignores rate limits).
    /// </summary>
    /// <param name="modelName">The model name to search for.</param>
    /// <returns>The provider, or null if not found.</returns>
    Task<Provider?> FindProviderForModelAsync(string modelName);

    /// <summary>
    ///     Finds the highest priority available provider for a model, considering rate limits.
    /// </summary>
    /// <param name="modelName">The model name to search for.</param>
    /// <param name="estimatedTokens">Estimated token count for the request.</param>
    /// <returns>The best available provider, or null if none available.</returns>
    Task<Provider?> FindAvailableProviderForModelAsync(string modelName, int estimatedTokens = 0);

    /// <summary>
    ///     Gets all available providers for a model, considering rate limits and estimated tokens.
    /// </summary>
    /// <param name="modelName">The model name to search for.</param>
    /// <param name="estimatedTokens">Estimated token count for the request.</param>
    /// <returns>A list of available providers ordered by priority.</returns>
    Task<List<Provider>> GetAllAvailableProvidersForModelAsync(string modelName, int estimatedTokens = 0);

    /// <summary>
    ///     Gets a virtual provider by name.
    /// </summary>
    /// <param name="providerName">The virtual provider name (default: "VanPatten").</param>
    /// <returns>The virtual provider, or null if not found.</returns>
    Task<Provider?> GetVirtualProviderAsync(string providerName = "VanPatten");

    /// <summary>
    ///     Gets all free models from active non-virtual providers for auto mode.
    /// </summary>
    /// <param name="virtualProviderName">The virtual provider name (default: "VanPatten").</param>
    /// <returns>A list of free models ordered by provider priority.</returns>
    Task<List<ProviderModel>> GetFreeModelsForAutoModeAsync(string virtualProviderName = "VanPatten");

    /// <summary>
    ///     Gets all models from active non-virtual providers for auto mode (all models, not just free).
    /// </summary>
    /// <param name="virtualProviderName">The virtual provider name (default: "VanPatten").</param>
    /// <returns>A list of all models ordered by provider priority.</returns>
    Task<List<ProviderModel>> GetAllModelsForAutoModeAsync(string virtualProviderName = "VanPatten");

    /// <summary>
    ///     Gets providers that offer a specific free model by exact model name.
    /// </summary>
    /// <param name="modelName">The exact model name to search for.</param>
    /// <param name="virtualProviderName">The virtual provider name (default: "VanPatten").</param>
    /// <returns>A list of ProviderModel entries (each with its provider) that offer this model.</returns>
    Task<List<ProviderModel>> GetProvidersForSpecificModelAsync(string modelName, string virtualProviderName = "VanPatten");

    /// <summary>
    ///     Resolves an auto provider for the given model name (supports virtual provider routing).
    /// </summary>
    /// <param name="modelName">The model name (e.g., "VanPatten/auto-free", "VanPatten/auto", or "VanPatten/google/gemini-2.0-flash-exp:free").</param>
    /// <param name="estimatedTokens">Estimated token count for the request.</param>
    /// <returns>A tuple of (resolved provider, mode, specificModel). Mode can be "normal", "auto-free", "auto", or "specific".</returns>
    Task<(Provider? Provider, string Mode, string? SpecificModel)> ResolveAutoProviderAsync(string modelName, int estimatedTokens = 0);

    /// <summary>
    ///     Records usage statistics for a provider.
    /// </summary>
    /// <param name="providerId">The provider ID.</param>
    /// <param name="tokensUsed">Number of tokens consumed.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task RecordUsageAsync(int providerId, int tokensUsed);

    /// <summary>
    ///     Resets daily and hourly usage counters for providers where the period has elapsed.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task ResetDailyUsageIfNeededAsync();
}

/// <summary>
///     Service for managing AI providers, models, and rate limiting.
/// </summary>
public class ProviderService : IProviderService
{
    private readonly AiRouterDbContext _context;
    private readonly ILogger<ProviderService> _logger;

    /// <summary>
    ///     Initializes a new instance of the <see cref="ProviderService" /> class.
    /// </summary>
    /// <param name="context">The database context.</param>
    /// <param name="logger">The logger.</param>
    public ProviderService(AiRouterDbContext context, ILogger<ProviderService> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<List<ProviderDto>> GetAllProvidersAsync()
    {
        _logger.LogDebug("Getting all providers");

        // Load providers first, then models separately to avoid multiple collection includes
        var providers = await _context.Providers
                                      .AsSplitQuery()
                                      .OrderByDescending(p => p.Priority)
                                      .ToListAsync();

        var providerIds = providers.Select(p => p.Id).ToList();
        var allModels = await _context.ProviderModels
                                      .Where(m => providerIds.Contains(m.ProviderId))
                                      .ToListAsync();

        var modelsByProvider = allModels.GroupBy(m => m.ProviderId).ToDictionary(g => g.Key, g => g.ToList());

        var result = providers.Select(p => new ProviderDto
        {
            Id = p.Id,
            Name = p.Name,
            BaseUrl = p.BaseUrl,
            ApiKey = p.ApiKey,
            Type = p.Type,
            Headers = ParseHeaders(p.Headers),
            IsActive = p.IsActive,
            Priority = p.Priority,
            IsVirtual = p.IsVirtual,
            RateLimit = p.RateLimit,
            UsageStats = new ProviderUsageStats
            {
                RequestsToday = p.RequestsToday,
                TokensToday = p.TokensToday,
                LastResetDate = p.LastResetDate,
                IsRateLimited = p.IsRateLimited
            },
            Models = modelsByProvider.TryGetValue(p.Id, out var models)
                ? models.Select(m => new ProviderModelDto
                {
                    Id = m.Id,
                    ModelName = m.ModelName,
                    ProviderModelName = m.ProviderModelName,
                    IsEnabled = m.IsEnabled
                }).ToList()
                : new List<ProviderModelDto>()
        }).ToList();

        _logger.LogInformation("Retrieved {Count} providers", result.Count);
        return result;
    }

    /// <inheritdoc />
    public async Task<ProviderDto?> GetProviderByIdAsync(int id)
    {
        _logger.LogDebug("Getting provider by ID: {ProviderId}", id);
        var provider = await _context.Providers
                                     .Include(p => p.Models)
                                     .FirstOrDefaultAsync(p => p.Id == id);

        if (provider == null)
        {
            _logger.LogWarning("Provider not found: {ProviderId}", id);
            return null;
        }

        _logger.LogInformation("Retrieved provider: {ProviderName} (ID: {ProviderId})", provider.Name, provider.Id);

        return new ProviderDto
        {
            Id = provider.Id,
            Name = provider.Name,
            BaseUrl = provider.BaseUrl,
            ApiKey = provider.ApiKey,
            Type = provider.Type,
            Headers = ParseHeaders(provider.Headers),
            IsActive = provider.IsActive,
            Priority = provider.Priority,
            RateLimit = provider.RateLimit,
            UsageStats = new ProviderUsageStats
            {
                RequestsToday = provider.RequestsToday,
                TokensToday = provider.TokensToday,
                LastResetDate = provider.LastResetDate,
                IsRateLimited = provider.IsRateLimited
            },
            Models = provider.Models.Select(m => new ProviderModelDto
            {
                Id = m.Id,
                ModelName = m.ModelName,
                ProviderModelName = m.ProviderModelName,
                IsEnabled = m.IsEnabled
            }).ToList()
        };
    }

    /// <inheritdoc />
    public async Task<ProviderDto> CreateProviderAsync(ProviderDto providerDto)
    {
        _logger.LogInformation("Creating new provider: {ProviderName}", providerDto.Name);

        var provider = new Provider
        {
            Name = providerDto.Name,
            BaseUrl = providerDto.BaseUrl,
            ApiKey = providerDto.ApiKey,
            Type = providerDto.Type,
            Headers = SerializeHeaders(providerDto.Headers),
            IsActive = providerDto.IsActive,
            Priority = providerDto.Priority,
            RateLimitJson = JsonSerializer.Serialize(providerDto.RateLimit),
            Models = providerDto.Models.Select(m => new ProviderModel
            {
                ModelName = m.ModelName,
                ProviderModelName = m.ProviderModelName,
                IsEnabled = m.IsEnabled
            }).ToList()
        };

        _context.Providers.Add(provider);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Created provider: {ProviderName} (ID: {ProviderId}) with {ModelCount} models",
                               provider.Name, provider.Id, provider.Models.Count);

        return await GetProviderByIdAsync(provider.Id) ?? providerDto;
    }

    /// <inheritdoc />
    public async Task<ProviderDto?> UpdateProviderAsync(int id, ProviderDto providerDto)
    {
        _logger.LogInformation("Updating provider: {ProviderId}", id);

        var provider = await _context.Providers
                                     .Include(p => p.Models)
                                     .FirstOrDefaultAsync(p => p.Id == id);

        if (provider == null)
        {
            _logger.LogWarning("Provider not found for update: {ProviderId}", id);
            return null;
        }

        var oldName = provider.Name;
        provider.Name = providerDto.Name;
        provider.BaseUrl = providerDto.BaseUrl;
        provider.ApiKey = providerDto.ApiKey;
        provider.Type = providerDto.Type;
        provider.Headers = SerializeHeaders(providerDto.Headers);
        provider.IsActive = providerDto.IsActive;
        provider.Priority = providerDto.Priority;
        provider.RateLimitJson = JsonSerializer.Serialize(providerDto.RateLimit);
        provider.UpdatedAt = DateTime.UtcNow;

        // Update models
        if (providerDto.Models != null)
        {
            _context.ProviderModels.RemoveRange(provider.Models);
            provider.Models = providerDto.Models.Select(m => new ProviderModel
            {
                ModelName = m.ModelName,
                ProviderModelName = m.ProviderModelName,
                IsEnabled = m.IsEnabled
            }).ToList();
        }
        else
        {
            // If Models is null, remove all models
            _context.ProviderModels.RemoveRange(provider.Models);
            provider.Models = new List<ProviderModel>();
        }

        await _context.SaveChangesAsync();

        _logger.LogInformation("Updated provider: {OldName} -> {NewName} (ID: {ProviderId}) with {ModelCount} models",
                               oldName, provider.Name, provider.Id, provider.Models.Count);

        return await GetProviderByIdAsync(id);
    }

    /// <inheritdoc />
    public async Task DeleteProviderAsync(int id)
    {
        _logger.LogInformation("Deleting provider: {ProviderId}", id);

        var provider = await _context.Providers.FindAsync(id);
        if (provider != null)
        {
            var name = provider.Name;
            _context.Providers.Remove(provider);
            await _context.SaveChangesAsync();
            _logger.LogInformation("Deleted provider: {ProviderName} (ID: {ProviderId})", name, id);
        }
        else
        {
            _logger.LogWarning("Provider not found for deletion: {ProviderId}", id);
        }
    }

    /// <inheritdoc />
    public async Task<List<ModelInfo>> GetAvailableModelsAsync()
    {
        _logger.LogDebug("Getting available models");

        // Reset daily/hourly usage if needed
        await ResetDailyUsageIfNeededAsync();

        var models = await _context.ProviderModels
                                   .Include(m => m.Provider)
                                   .Where(m => m.IsEnabled && m.Provider.IsActive)
                                   .OrderByDescending(m => m.Provider.Priority)
                                   .Select(m => new { m.ModelName, m.Provider, m.IsFree })
                                   .ToListAsync();

        // Filter out rate-limited providers in memory using IsRateLimited property
        var availableModels = models
                             .Where(m => !m.Provider.IsRateLimited)
                             .Select(m => new ModelInfo
                              {
                                  Id = m.ModelName,
                                  OwnedBy = m.Provider.Name,
                                  IsFree = m.IsFree,
                                  Provider = m.Provider.Name
                              })
                             .ToList();

        var distinctModels = availableModels.DistinctBy(m => m.Id).ToList();
        _logger.LogInformation("Retrieved {Count} available models", distinctModels.Count);

        return distinctModels;
    }

    /// <inheritdoc />
    public async Task<ModelsGroupedResponse> GetGroupedModelsAsync()
    {
        _logger.LogDebug("Getting grouped available models");

        // Reset daily/hourly usage if needed
        await ResetDailyUsageIfNeededAsync();

        var models = await _context.ProviderModels
                                   .Include(m => m.Provider)
                                   .Where(m => m.IsEnabled && m.Provider.IsActive)
                                   .OrderByDescending(m => m.Provider.Priority)
                                   .Select(m => new { m.ModelName, m.Provider, m.IsFree })
                                   .ToListAsync();

        // Filter out rate-limited providers in memory using IsRateLimited property
        var availableModels = models
                             .Where(m => !m.Provider.IsRateLimited)
                             .ToList();

        // Get distinct models (by ModelName) with their providers and free status
        var distinctModels = availableModels
                            .GroupBy(m => m.ModelName)
                            .Select(g => new
                             {
                                 ModelName = g.Key,
                                 IsFree = g.Any(x => x.IsFree), // Free if any provider offers it free
                                 Providers = g.Select(x => new { x.Provider.Name, x.IsFree }).ToList()
                             })
                            .ToList();

        var response = new ModelsGroupedResponse();

        // Group 1: Free models
        var freeModels = distinctModels
                        .Where(m => m.IsFree)
                        .OrderBy(m => m.ModelName)
                        .Select(m => new ModelInfo
                         {
                             Id = m.ModelName,
                             Object = "model",
                             Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                             OwnedBy = string.Join(", ", m.Providers.Select(p => p.Name).Distinct()),
                             IsFree = true,
                             Provider = string.Join(", ", m.Providers.Select(p => p.Name).Distinct())
                         })
                        .ToList();

        if (freeModels.Count > 0)
            response.Groups.Add(new ModelGroup
            {
                Name = "Free Models",
                Type = "free",
                Provider = "Multiple",
                Models = freeModels
            });

        // Group 2+: Models by provider
        var providerGroups = availableModels
                            .GroupBy(m => m.Provider.Name)
                            .OrderByDescending(g => g.First().Provider.Priority)
                            .ToList();

        foreach (var providerGroup in providerGroups)
        {
            var providerName = providerGroup.Key;
            var providerPriority = providerGroup.First().Provider.Priority;

            var providerModels = providerGroup
                                .GroupBy(m => m.ModelName)
                                .Select(g => new ModelInfo
                                 {
                                     Id = g.Key,
                                     Object = "model",
                                     Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                                     OwnedBy = providerName,
                                     IsFree = g.Any(x => x.IsFree),
                                     Provider = providerName
                                 })
                                .OrderBy(m => m.Id)
                                .ToList();

            response.Groups.Add(new ModelGroup
            {
                Name = providerName,
                Type = "provider",
                Provider = providerName,
                Models = providerModels
            });
        }

        _logger.LogInformation("Retrieved {GroupCount} model groups with {TotalModels} models",
                               response.Groups.Count, response.Groups.Sum(g => g.Models.Count));

        return response;
    }

    /// <inheritdoc />
    public async Task<Provider?> FindProviderForModelAsync(string modelName)
    {
        _logger.LogDebug("Finding provider for model: {ModelName}", modelName);

        // Match exact or by suffix (e.g., "openai/gpt-4o-mini" matches "gpt-4o-mini")
        var providerModel = await _context.ProviderModels
                                          .Include(m => m.Provider)
                                          .Where(m => (m.ModelName == modelName
                                                         || m.ModelName.EndsWith("/" + modelName)) && m.IsEnabled
                                                     && m.Provider.IsActive)
                                          .OrderByDescending(m => m.Provider.Priority)
                                          .FirstOrDefaultAsync();

        if (providerModel?.Provider != null)
            _logger.LogInformation("Found provider {ProviderName} for model {ModelName}",
                                   providerModel.Provider.Name, modelName);
        else
            _logger.LogWarning("No provider found for model: {ModelName}", modelName);

        return providerModel?.Provider;
    }

/// <inheritdoc />
    public async Task<Provider?> FindAvailableProviderForModelAsync(string modelName, int estimatedTokens = 0)
    {
        _logger.LogDebug("Finding available provider for model: {ModelName} (estimated tokens: {Tokens})", modelName,
                         estimatedTokens);

        // Reset daily/hourly usage if needed
        await ResetDailyUsageIfNeededAsync();

        // Match exact or by suffix (e.g., "openai/gpt-4o-mini" matches "gpt-4o-mini")
        var providers = await _context.ProviderModels
                                       .Include(m => m.Provider)
                                       .Where(m => (m.ModelName == modelName || m.ModelName.EndsWith("/" + modelName))
                                                  && m.IsEnabled && m.Provider.IsActive)
                                       .OrderByDescending(m => m.Provider.Priority)
                                       .Select(m => m.Provider)
                                       .ToListAsync();

        foreach (var provider in providers)
        {
            // Check rate limits using IsRateLimited property (which now relies on pre-reset)
            if (!provider.IsRateLimited)
            {
                // Check if estimated tokens would exceed limit
                var rateLimit = provider.RateLimit;
                if (estimatedTokens > 0)
                {
                    if (rateLimit.RequestsPerDay > 0)
                    {
                        // Daily limit - check both requests and tokens
                        if (provider.RequestsToday + 1 > rateLimit.RequestsPerDay ||
                            provider.TokensToday + estimatedTokens > rateLimit.TokensPerDay)
                        {
                            _logger.LogWarning(
                                "Provider {ProviderName} would exceed daily limit with estimated {Tokens} tokens",
                                provider.Name, estimatedTokens);
                            continue;
                        }
                    }
                    else
                    {
                        // Hourly limit - check both requests and tokens
                        if (provider.RequestsThisHour + 1 > rateLimit.RequestsPerHour ||
                            provider.TokensThisHour + estimatedTokens > rateLimit.TokensPerHour)
                        {
                            _logger.LogWarning(
                                "Provider {ProviderName} would exceed hourly limit with estimated {Tokens} tokens",
                                provider.Name, estimatedTokens);
                            continue;
                        }
                    }
                }

                _logger.LogInformation("Selected provider {ProviderName} for model {ModelName} (priority: {Priority})",
                                       provider.Name, modelName, provider.Priority);
                return provider;
            }

            _logger.LogDebug("Skipping rate-limited provider: {ProviderName}", provider.Name);
        }

        _logger.LogWarning("No available provider found for model: {ModelName} (all rate limited)", modelName);
        return null;
    }

/// <inheritdoc />
    public async Task<List<Provider>> GetAllAvailableProvidersForModelAsync(string modelName, int estimatedTokens = 0)
    {
        _logger.LogDebug("Getting all available providers for model: {ModelName} (estimated tokens: {Tokens})",
                         modelName, estimatedTokens);

        // Reset daily/hourly usage if needed
        await ResetDailyUsageIfNeededAsync();

        // Match exact or by suffix (e.g., "openai/gpt-4o-mini" matches "gpt-4o-mini")
        var providers = await _context.ProviderModels
                                       .Include(m => m.Provider)
                                       .Where(m => (m.ModelName == modelName || m.ModelName.EndsWith("/" + modelName))
                                                  && m.IsEnabled && m.Provider.IsActive)
                                       .OrderByDescending(m => m.Provider.Priority)
                                       .Select(m => m.Provider)
                                       .ToListAsync();

        var availableProviders = new List<Provider>();

        foreach (var provider in providers)
            // Check rate limits using IsRateLimited property
            if (!provider.IsRateLimited)
            {
                // Check if estimated tokens would exceed limit
                var rateLimit = provider.RateLimit;
                if (estimatedTokens > 0)
                {
                    if (rateLimit.RequestsPerDay > 0)
                    {
                        // Daily limit - check both requests and tokens
                        if (provider.RequestsToday + 1 > rateLimit.RequestsPerDay ||
                            provider.TokensToday + estimatedTokens > rateLimit.TokensPerDay)
                        {
                            _logger.LogWarning(
                                "Provider {ProviderName} would exceed daily limit with estimated {Tokens} tokens",
                                provider.Name, estimatedTokens);
                            continue;
                        }
                    }
                    else
                    {
                        // Hourly limit - check both requests and tokens
                        if (provider.RequestsThisHour + 1 > rateLimit.RequestsPerHour ||
                            provider.TokensThisHour + estimatedTokens > rateLimit.TokensPerHour)
                        {
                            _logger.LogWarning(
                                "Provider {ProviderName} would exceed hourly limit with estimated {Tokens} tokens",
                                provider.Name, estimatedTokens);
                            continue;
                        }
                    }
                }

                availableProviders.Add(provider);
                _logger.LogDebug("Provider {ProviderName} available for model {ModelName} (priority: {Priority})",
                                 provider.Name, modelName, provider.Priority);
            }
            else
            {
                _logger.LogDebug("Skipping rate-limited provider: {ProviderName}", provider.Name);
            }

        _logger.LogInformation("Found {Count} available providers for model: {ModelName}", availableProviders.Count,
                               modelName);
        return availableProviders;
    }

    /// <inheritdoc />
    public async Task RecordUsageAsync(int providerId, int tokensUsed)
    {
        var now = DateTime.UtcNow;
        var today = now.Date;
        var thisHour = new DateTime(now.Year, now.Month, now.Day, now.Hour, 0, 0, DateTimeKind.Utc);

        var isRelational = _context.Database.IsRelational();

        if (isRelational)
        {
            // Use ExecuteUpdateAsync for atomic database-level increment to avoid race conditions
            await _context.Database.ExecuteSqlRawAsync(
                @"
                UPDATE Providers 
                SET 
                    RequestsToday = CASE WHEN LastResetDate != @today THEN 1 ELSE RequestsToday + 1 END,
                    TokensToday = CASE WHEN LastResetDate != @today THEN @tokens ELSE TokensToday + @tokens END,
                    LastResetDate = CASE WHEN LastResetDate != @today THEN @today ELSE LastResetDate END,
                    RequestsThisHour = CASE WHEN LastHourReset != @thisHour THEN 1 ELSE RequestsThisHour + 1 END,
                    TokensThisHour = CASE WHEN LastHourReset != @thisHour THEN @tokens ELSE TokensThisHour + @tokens END,
                    LastHourReset = CASE WHEN LastHourReset != @thisHour THEN @thisHour ELSE LastHourReset END
                WHERE Id = @providerId",
                new[]
                {
                    new Microsoft.Data.Sqlite.SqliteParameter("@providerId", providerId),
                    new Microsoft.Data.Sqlite.SqliteParameter("@tokens", tokensUsed),
                    new Microsoft.Data.Sqlite.SqliteParameter("@today", today),
                    new Microsoft.Data.Sqlite.SqliteParameter("@thisHour", thisHour)
                });

            _logger.LogDebug(
                "Recorded usage atomically for provider {ProviderId}: +1 request, +{Tokens} tokens",
                providerId, tokensUsed);
        }
        else
        {
            // Fallback for in-memory database (tests) - use entity-based approach
            var provider = await _context.Providers.FindAsync(providerId);
            if (provider != null)
            {
                // Reset daily counters if needed
                if (provider.LastResetDate != today)
                {
                    provider.RequestsToday = 0;
                    provider.TokensToday = 0;
                    provider.LastResetDate = today;
                }

                // Reset hourly counters if needed
                if (provider.LastHourReset != thisHour)
                {
                    provider.RequestsThisHour = 0;
                    provider.TokensThisHour = 0;
                    provider.LastHourReset = thisHour;
                }

                // Increment daily counters
                provider.RequestsToday++;
                provider.TokensToday += tokensUsed;

                // Increment hourly counters
                provider.RequestsThisHour++;
                provider.TokensThisHour += tokensUsed;

                await _context.SaveChangesAsync();

                _logger.LogDebug(
                    "Recorded usage for provider {ProviderId}: +1 request, +{Tokens} tokens (in-memory fallback)",
                    providerId, tokensUsed);
            }
        }
    }

/// <inheritdoc />
    public async Task ResetDailyUsageIfNeededAsync()
    {
        var now = DateTime.UtcNow;
        var today = now.Date;
        var thisHour = new DateTime(now.Year, now.Month, now.Day, now.Hour, 0, 0, DateTimeKind.Utc);

        // Check if we're using a relational database (SQLite) or in-memory
        var isRelational = _context.Database.IsRelational();

        if (isRelational)
        {
            // Use atomic SQL updates to avoid race conditions with concurrent RecordUsageAsync calls
            var dailyResetCount = await _context.Database.ExecuteSqlRawAsync(
                @"
                UPDATE Providers 
                SET RequestsToday = 0, TokensToday = 0, LastResetDate = @today
                WHERE LastResetDate < @today",
                new Microsoft.Data.Sqlite.SqliteParameter("@today", today));

            var hourlyResetCount = await _context.Database.ExecuteSqlRawAsync(
                @"
                UPDATE Providers 
                SET RequestsThisHour = 0, TokensThisHour = 0, LastHourReset = @thisHour
                WHERE LastHourReset < @thisHour",
                new Microsoft.Data.Sqlite.SqliteParameter("@thisHour", thisHour));

            if (dailyResetCount > 0)
                _logger.LogInformation("Reset daily usage for {Count} providers", dailyResetCount);

            if (hourlyResetCount > 0)
                _logger.LogInformation("Reset hourly usage for {Count} providers", hourlyResetCount);
        }
        else
        {
            // Fallback for in-memory database (tests) - use entity-based approach
            var providersToReset = await _context.Providers
                                                  .Where(p => p.LastResetDate < today || p.LastHourReset < thisHour)
                                                  .ToListAsync();

            if (providersToReset.Count > 0)
            {
                foreach (var provider in providersToReset)
                {
                    // Reset daily if needed
                    if (provider.LastResetDate < today)
                    {
                        provider.RequestsToday = 0;
                        provider.TokensToday = 0;
                        provider.LastResetDate = today;
                        _logger.LogDebug("Reset daily usage for provider: {ProviderName}", provider.Name);
                    }

                    // Reset hourly if needed
                    if (provider.LastHourReset < thisHour)
                    {
                        provider.RequestsThisHour = 0;
                        provider.TokensThisHour = 0;
                        provider.LastHourReset = thisHour;
                        _logger.LogDebug("Reset hourly usage for provider: {ProviderName}", provider.Name);
                    }
                }

                await _context.SaveChangesAsync();
                
                if (providersToReset.Count > 0)
                    _logger.LogInformation("Reset usage for {Count} providers (in-memory fallback)", providersToReset.Count);
            }
        }
    }

    /// <inheritdoc />
    public async Task<Provider?> GetVirtualProviderAsync(string providerName = "VanPatten")
    {
        _logger.LogDebug("Getting virtual provider: {ProviderName}", providerName);

        var provider = await _context.Providers
                                     .Include(p => p.Models)
                                     .FirstOrDefaultAsync(p => p.Name == providerName && p.IsVirtual && p.IsActive);

        if (provider != null)
            _logger.LogInformation("Found virtual provider: {ProviderName}", provider.Name);
        else
            _logger.LogWarning("Virtual provider not found: {ProviderName}", providerName);

        return provider;
    }

    /// <inheritdoc />
    public async Task<List<ProviderModel>> GetFreeModelsForAutoModeAsync(string virtualProviderName = "VanPatten")
    {
        _logger.LogDebug("Getting free models for auto mode from virtual provider: {ProviderName}",
                         virtualProviderName);

        var virtualProvider = await GetVirtualProviderAsync(virtualProviderName);
        if (virtualProvider == null) return new List<ProviderModel>();

        // Get all free models from all active non-virtual providers
        var freeModels = await _context.ProviderModels
                                       .Include(m => m.Provider)
                                       .Where(m => m.IsEnabled
                                                  && m.IsFree
                                                  && m.Provider.IsActive
                                                  && !m.Provider.IsVirtual
                                                  && m.Provider.Type != "virtual")
                                       .OrderByDescending(m => m.Provider.Priority)
                                       .ToListAsync();

        _logger.LogInformation("Found {Count} free models for auto mode", freeModels.Count);
        return freeModels;
    }

    /// <inheritdoc />
    public async Task<List<ProviderModel>> GetAllModelsForAutoModeAsync(string virtualProviderName = "VanPatten")
    {
        _logger.LogDebug("Getting all models for auto mode from virtual provider: {ProviderName}",
                         virtualProviderName);

        var virtualProvider = await GetVirtualProviderAsync(virtualProviderName);
        if (virtualProvider == null) return new List<ProviderModel>();

        // Get all models from all active non-virtual providers
        var allModels = await _context.ProviderModels
                                       .Include(m => m.Provider)
                                       .Where(m => m.IsEnabled
                                                  && m.Provider.IsActive
                                                  && !m.Provider.IsVirtual
                                                  && m.Provider.Type != "virtual")
                                       .OrderByDescending(m => m.Provider.Priority)
                                       .ToListAsync();

        _logger.LogInformation("Found {Count} models for auto mode", allModels.Count);
        return allModels;
    }

/// <inheritdoc />
    public async Task<List<ProviderModel>> GetProvidersForSpecificModelAsync(string modelName, string virtualProviderName = "VanPatten")
    {
        _logger.LogDebug("Getting providers for specific model: {ModelName}", modelName);

        var virtualProvider = await GetVirtualProviderAsync(virtualProviderName);
        if (virtualProvider == null) return new List<ProviderModel>();

        // Get all providers that have this specific model (enabled, any free/paid)
        // Use exact match with proper path boundary check to avoid false positives
        var models = await _context.ProviderModels
                                     .Include(m => m.Provider)
                                     .Where(m => m.IsEnabled
                                                && m.ModelName == modelName
                                                && m.Provider.IsActive
                                                && !m.Provider.IsVirtual
                                                && m.Provider.Type != "virtual")
                                     .OrderByDescending(m => m.Provider.Priority)
                                     .ToListAsync();

        _logger.LogInformation("Found {Count} providers for model {ModelName}", models.Count, modelName);
        return models;
    }

    /// <inheritdoc />
    public async Task<(Provider? Provider, string Mode, string? SpecificModel)> ResolveAutoProviderAsync(string modelName, int estimatedTokens = 0)
    {
        _logger.LogDebug("Resolving auto provider for model: {ModelName}", modelName);

        // Check if it's a virtual provider request (VanPatten/...)
        if (modelName.StartsWith("VanPatten/") || modelName.StartsWith("VanPatten\\"))
        {
            var parts = modelName.Split(new[] { '/', '\\' }, 2);
            var virtualProviderName = parts[0];
            var requestedPath = parts.Length > 1 ? parts[1] : "auto-free";

            _logger.LogInformation("Virtual provider request: {VirtualProvider}/{Path}", virtualProviderName,
                                   requestedPath);

            var virtualProvider = await GetVirtualProviderAsync(virtualProviderName);
            if (virtualProvider == null)
            {
                _logger.LogWarning("Virtual provider not found: {VirtualProviderName}", virtualProviderName);
                return (null, requestedPath, null);
            }

            // Check if it's a known mode
            if (requestedPath == "auto-free" || requestedPath == "auto")
            {
                // Known modes: auto-free or auto - reset usage and check if any underlying provider is available
                await ResetDailyUsageIfNeededAsync();
                return (virtualProvider, requestedPath, null);
            }

            // Third mode: specific model (e.g., vanpatten/google/gemini-2.0-flash-exp:free)
            // The requestedPath is the model name - check if any provider has it and is not rate limited
            var specificModel = requestedPath;
            _logger.LogInformation("Specific model mode requested: {Model}", specificModel);
            
            // Check if any provider has this model and is available
            var availableProviders = await GetAllAvailableProvidersForModelAsync(specificModel, estimatedTokens);
            if (availableProviders.Count == 0)
            {
                _logger.LogWarning("No available provider for specific model: {Model}", specificModel);
                return (null, "specific", specificModel);
            }
            
            return (virtualProvider, "specific", specificModel);
        }

        // For regular models, use existing logic
        var provider = await FindAvailableProviderForModelAsync(modelName, estimatedTokens);
        return (provider, "normal", null);
    }

    /// <summary>
    ///     Parses headers from JSON string or key:value format.
    /// </summary>
    /// <param name="headersJson">The headers as JSON string or comma-separated key:value pairs.</param>
    /// <returns>A dictionary of headers, or null if input is empty.</returns>
    private static Dictionary<string, string>? ParseHeaders(string? headersJson)
    {
        if (string.IsNullOrWhiteSpace(headersJson)) return null;

        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, string>>(headersJson);
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

            return dict.Count > 0 ? dict : null;
        }
    }

    /// <summary>
    ///     Serializes headers dictionary to JSON string.
    /// </summary>
    /// <param name="headers">The headers dictionary.</param>
    /// <returns>The JSON string, or null if empty.</returns>
    private static string? SerializeHeaders(Dictionary<string, string>? headers)
    {
        if (headers == null || headers.Count == 0) return null;

        return JsonSerializer.Serialize(headers);
    }
}