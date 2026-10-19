namespace VanPattenAIRouter.Models;

/// <summary>
///     Configuration for a provider loaded from appsettings.json.
/// </summary>
public class ProviderConfig
{
    /// <summary>
    ///     Display name of the provider.
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>
    ///     Base URL of the provider's API endpoint.
    /// </summary>
    public string BaseUrl { get; set; } = "";

    /// <summary>
    ///     API key for authenticating with the provider. Can be an environment variable reference like ${ENV_VAR}.
    /// </summary>
    public string ApiKey { get; set; } = "";

    /// <summary>
    ///     Type of the provider (e.g., "openai", "anthropic", "virtual").
    /// </summary>
    public string Type { get; set; } = "openai";

    /// <summary>
    ///     Additional HTTP headers to include in requests to this provider.
    /// </summary>
    public Dictionary<string, string>? Headers { get; set; }

    /// <summary>
    ///     Whether the provider is currently active.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    ///     Priority of the provider for routing decisions.
    /// </summary>
    public int Priority { get; set; } = 0;

    /// <summary>
    ///     Whether this is a virtual provider (router).
    /// </summary>
    public bool IsVirtual { get; set; } = false;

    /// <summary>
    ///     Rate limiting configuration for this provider.
    /// </summary>
    public RateLimitConfig RateLimit { get; set; } = new();

    /// <summary>
    ///     List of models for virtual providers.
    /// </summary>
    public List<ProviderModelConfig> Models { get; set; } = new();
}