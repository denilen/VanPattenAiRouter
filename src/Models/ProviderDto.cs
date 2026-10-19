namespace VanPattenAIRouter.Models;

/// <summary>
///     Data transfer object for a provider configuration.
/// </summary>
public class ProviderDto
{
    /// <summary>
    ///     Unique identifier for the provider.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    ///     Display name of the provider.
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>
    ///     Base URL of the provider's API endpoint.
    /// </summary>
    public string BaseUrl { get; set; } = "";

    /// <summary>
    ///     API key for authenticating with the provider.
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
    ///     Whether the provider is currently active and available for routing.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    ///     Priority of the provider for routing decisions. Higher priority providers are tried first.
    /// </summary>
    public int Priority { get; set; } = 0;

    /// <summary>
    ///     Whether this is a virtual provider (router) that doesn't have a direct API endpoint.
    /// </summary>
    public bool IsVirtual { get; set; } = false;

    /// <summary>
    ///     Rate limiting configuration for this provider.
    /// </summary>
    public RateLimitConfig RateLimit { get; set; } = new();

    /// <summary>
    ///     List of models offered by this provider.
    /// </summary>
    public List<ProviderModelDto> Models { get; set; } = new();

    /// <summary>
    ///     Current usage statistics for this provider.
    /// </summary>
    public ProviderUsageStats UsageStats { get; set; } = new();
}