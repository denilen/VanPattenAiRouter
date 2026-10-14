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

/// <summary>
///     Data transfer object for a provider model.
/// </summary>
public class ProviderModelDto
{
    /// <summary>
    ///     Unique identifier for the provider model.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    ///     The model name as exposed by the router.
    /// </summary>
    public string ModelName { get; set; } = "";

    /// <summary>
    ///     The model name as known by the provider (may differ from ModelName).
    /// </summary>
    public string? ProviderModelName { get; set; }

    /// <summary>
    ///     Whether this model is currently enabled.
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    ///     Whether this model is free to use (does not consume credits).
    /// </summary>
    public bool IsFree { get; set; } = false;

    /// <summary>
    ///     The mode of the model. Can be "normal", "auto-free", or "auto-all".
    /// </summary>
    public string Mode { get; set; } = "normal";
}

/// <summary>
///     Configuration for rate limiting a provider.
/// </summary>
public class RateLimitConfig
{
    /// <summary>
    ///     Maximum number of requests allowed per day. Set to 0 to disable daily limit.
    /// </summary>
    public int RequestsPerDay { get; set; } = 1000;

    /// <summary>
    ///     Maximum number of tokens allowed per day. Set to 0 to disable daily token limit.
    /// </summary>
    public int TokensPerDay { get; set; } = 100000;

    /// <summary>
    ///     Maximum number of requests allowed per hour. Set to 0 to disable hourly limit.
    /// </summary>
    public int RequestsPerHour { get; set; } = 0;

    /// <summary>
    ///     Maximum number of tokens allowed per hour. Set to 0 to disable hourly token limit.
    /// </summary>
    public int TokensPerHour { get; set; } = 0;
}

/// <summary>
///     Current usage statistics for a provider.
/// </summary>
public class ProviderUsageStats
{
    /// <summary>
    ///     Number of requests made today.
    /// </summary>
    public int RequestsToday { get; set; }

    /// <summary>
    ///     Number of tokens consumed today.
    /// </summary>
    public int TokensToday { get; set; }

    /// <summary>
    ///     The date when the daily counters were last reset.
    /// </summary>
    public DateTime LastResetDate { get; set; } = DateTime.UtcNow.Date;

    /// <summary>
    ///     Whether the provider is currently rate limited.
    /// </summary>
    public bool IsRateLimited { get; set; }
}

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

/// <summary>
///     Configuration for a model in a virtual provider.
/// </summary>
public class ProviderModelConfig
{
    /// <summary>
    ///     The model name as exposed by the router.
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>
    ///     Human-readable description of the model.
    /// </summary>
    public string Description { get; set; } = "";

    /// <summary>
    ///     The mode of the model. Can be "normal", "auto-free", or "auto-all".
    /// </summary>
    public string Mode { get; set; } = "normal";

    /// <summary>
    ///     Whether this model is enabled.
    /// </summary>
    public bool IsEnabled { get; set; } = true;
}

/// <summary>
///     Configuration for retry policies.
/// </summary>
public class RetryConfig
{
    /// <summary>
    ///     Maximum number of retry attempts.
    /// </summary>
    public int MaxAttempts { get; set; } = 3;

    /// <summary>
    ///     Base delay in seconds between retry attempts.
    /// </summary>
    public int BaseDelaySeconds { get; set; } = 2;

    /// <summary>
    ///     Maximum delay in seconds between retry attempts.
    /// </summary>
    public int MaxDelaySeconds { get; set; } = 30;

    /// <summary>
    ///     Whether to use exponential backoff for retry delays.
    /// </summary>
    public bool ExponentialBackoff { get; set; } = true;

    /// <summary>
    ///     List of HTTP status codes that should trigger a retry.
    /// </summary>
    public List<int> RetryableStatusCodes { get; set; } = new() { 408, 429, 500, 502, 503, 504 };

    /// <summary>
    ///     List of exception type names that should trigger a retry.
    /// </summary>
    public List<string> RetryableExceptions { get; set; } = new()
        { "HttpRequestException", "TaskCanceledException", "TimeoutException" };
}