namespace VanPattenAIRouter.Models;

/// <summary>
///     Configuration options for the AI Router.
/// </summary>
public class RouterOptions
{
    /// <summary>
    ///     Default provider to use when none specified.
    /// </summary>
    public string DefaultProvider { get; set; } = "";

    /// <summary>
    ///     Request timeout in seconds.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 120;

    /// <summary>
    ///     Interval in minutes for refreshing model lists from providers.
    /// </summary>
    public int ModelRefreshIntervalMinutes { get; set; } = 10;

    /// <summary>
    ///     Free model patterns by provider prefix. Key = provider name prefix (e.g., "KiloCode", "NVIDIA", "OpenRouter"),
    ///     Value = list of model name patterns that indicate a free model.
    /// </summary>
    public Dictionary<string, List<string>> FreeModels { get; set; } = new();

    /// <summary>
    ///     Retry policy configuration.
    /// </summary>
    public RetryConfig Retry { get; set; } = new();

    /// <summary>
    ///     Provider configurations for seeding.
    /// </summary>
    public List<ProviderConfig> Providers { get; set; } = new();
}