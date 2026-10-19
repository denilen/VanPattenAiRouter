using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using VanPattenAIRouter.Models;

namespace VanPattenAIRouter.Data;

/// <summary>
///     Handles database seeding for the AI Router application.
/// </summary>
public static class SeedData
{
    /// <summary>
    ///     Initializes the database with providers from configuration or defaults.
    /// </summary>
    /// <param name="context">The database context.</param>
    /// <param name="appOptions">Application options containing provider configuration.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public static async Task InitializeAsync(AiRouterDbContext context, IOptions<AppOptions> appOptions)
    {
        if (await context.Providers.AnyAsync()) return; // DB has been seeded

        var providers = new List<Provider>();
        var routerOptions = appOptions?.Value?.Router;

        if (routerOptions?.Providers != null && routerOptions.Providers.Count > 0)
            foreach (var config in routerOptions.Providers)
            {
                var apiKey = ResolveApiKey(config.ApiKey);
                var provider = new Provider
                {
                    Name = config.Name,
                    BaseUrl = config.BaseUrl,
                    ApiKey = apiKey,
                    Type = config.Type,
                    Headers = config.Headers?.Count > 0 ? JsonSerializer.Serialize(config.Headers) : null,
                    IsActive = config.IsActive,
                    Priority = config.Priority,
                    IsVirtual = config.IsVirtual,
                    RateLimitJson = JsonSerializer.Serialize(config.RateLimit),
                    Models = new List<ProviderModel>()
                };

                // Add virtual provider models from config
                if (config.IsVirtual && config.Models != null)
                    foreach (var modelConfig in config.Models.Where(m => m.IsEnabled))
                        provider.Models.Add(new ProviderModel
                        {
                            ModelName = modelConfig.Name,
                            ProviderModelName = modelConfig.Name,
                            IsEnabled = true,
                            IsFree = modelConfig.Mode == "auto-free",
                            Mode = modelConfig.Mode
                        });

                providers.Add(provider);
            }

        // Fallback to environment variables if no config
        if (providers.Count == 0) providers.AddRange(GetDefaultProviders());

        if (providers.Count > 0)
        {
            context.Providers.AddRange(providers);
            await context.SaveChangesAsync();
        }
    }

    /// <summary>
    ///     Resolves an API key from configuration, supporting environment variable references.
    /// </summary>
    /// <param name="apiKey">The API key or environment variable reference (e.g., ${ENV_VAR}).</param>
    /// <returns>The resolved API key, or empty string if not found.</returns>
    private static string ResolveApiKey(string? apiKey)
    {
        if (string.IsNullOrWhiteSpace(apiKey)) return "";

        // Check if it's an environment variable reference: ${ENV_VAR_NAME}
        if (apiKey.StartsWith("${") && apiKey.EndsWith("}"))
        {
            var envVarName = apiKey[2..^1];
            return Environment.GetEnvironmentVariable(envVarName) ?? "";
        }

        return apiKey;
    }

    /// <summary>
    ///     Gets default providers when no configuration is provided.
    /// </summary>
    /// <returns>A list of default providers.</returns>
    private static List<Provider> GetDefaultProviders()
    {
        // Minimal defaults - only used if no config at all
        return new List<Provider>
        {
            new()
            {
                Name = "OpenAI",
                BaseUrl = "https://api.openai.com/v1",
                ApiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY") ?? "",
                Type = "openai",
                IsActive = true,
                Priority = 100,
                RateLimitJson
                    = JsonSerializer.Serialize(new RateLimitConfig { RequestsPerDay = 500, TokensPerDay = 100000 }),
                Models = new List<ProviderModel>()
            }
        };
    }
}