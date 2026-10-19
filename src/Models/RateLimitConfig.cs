namespace VanPattenAIRouter.Models;

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