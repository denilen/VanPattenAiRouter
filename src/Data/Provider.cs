using System.Text.Json;
using VanPattenAIRouter.Models;

namespace VanPattenAIRouter.Data;

/// <summary>
///     Represents an AI provider in the system.
/// </summary>
public class Provider
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
    ///     Additional HTTP headers as JSON string.
    /// </summary>
    public string? Headers { get; set; }

    /// <summary>
    ///     Whether the provider is currently active.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    ///     Priority for routing decisions. Higher priority providers are tried first.
    /// </summary>
    public int Priority { get; set; }

    /// <summary>
    ///     Timestamp when the provider was created.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    ///     Timestamp when the provider was last updated.
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    ///     Whether this is a virtual provider (router) without a direct API endpoint.
    /// </summary>
    public bool IsVirtual { get; set; }

    /// <summary>
    ///     Rate limit configuration serialized as JSON.
    /// </summary>
    public string RateLimitJson { get; set; } = "{}";

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
    ///     Number of requests made in the current hour.
    /// </summary>
    public int RequestsThisHour { get; set; }

    /// <summary>
    ///     Number of tokens consumed in the current hour.
    /// </summary>
    public int TokensThisHour { get; set; }

    /// <summary>
    ///     The hour when the hourly counters were last reset (UTC).
    /// </summary>
    public DateTime LastHourReset { get; set; } = DateTime.MinValue;

    /// <summary>
    ///     Number of requests made in the current minute.
    /// </summary>
    public int RequestsThisMinute { get; set; }

    /// <summary>
    ///     Number of tokens consumed in the current minute.
    /// </summary>
    public int TokensThisMinute { get; set; }

    /// <summary>
    ///     The minute when the per-minute counters were last reset (UTC).
    /// </summary>
    public DateTime LastMinuteReset { get; set; } = DateTime.MinValue;

    /// <summary>
    ///     Navigation property for models offered by this provider.
    /// </summary>
    public List<ProviderModel> Models { get; set; } = new();

    /// <summary>
    ///     Gets or sets the rate limit configuration.
    /// </summary>
    public RateLimitConfig RateLimit
    {
        get => JsonSerializer.Deserialize<RateLimitConfig>(RateLimitJson) ?? new RateLimitConfig();
        set => RateLimitJson = JsonSerializer.Serialize(value);
    }

    /// <summary>
    ///     Gets a value indicating whether the provider has exceeded its rate limits.
    ///     This property does NOT perform lazy reset - call ResetIfNeeded() on the service instead.
    ///     Checks all configured windows (day/hour/minute): provider is limited if any enabled limit is exceeded.
    /// </summary>
    public bool IsRateLimited
    {
        get
        {
            var rateLimit = RateLimit;

            if (rateLimit.RequestsPerDay > 0 &&
                (RequestsToday >= rateLimit.RequestsPerDay ||
                 (rateLimit.TokensPerDay > 0 && TokensToday >= rateLimit.TokensPerDay)))
                return true;

            if (rateLimit.RequestsPerHour > 0 &&
                (RequestsThisHour >= rateLimit.RequestsPerHour ||
                 (rateLimit.TokensPerHour > 0 && TokensThisHour >= rateLimit.TokensPerHour)))
                return true;

            if (rateLimit.RequestsPerMinute > 0 &&
                (RequestsThisMinute >= rateLimit.RequestsPerMinute ||
                 (rateLimit.TokensPerMinute > 0 && TokensThisMinute >= rateLimit.TokensPerMinute)))
                return true;

            // Token-only limits (if configured without request limit)
            if (rateLimit.RequestsPerDay == 0 && rateLimit.TokensPerDay > 0 && TokensToday >= rateLimit.TokensPerDay)
                return true;
            if (rateLimit.RequestsPerHour == 0 && rateLimit.TokensPerHour > 0 && TokensThisHour >= rateLimit.TokensPerHour)
                return true;
            if (rateLimit.RequestsPerMinute == 0 && rateLimit.TokensPerMinute > 0 && TokensThisMinute >= rateLimit.TokensPerMinute)
                return true;

            return false;
        }
    }

    /// <summary>
    ///     Checks if a new day has started since last reset.
    /// </summary>
    public bool IsDailyResetNeeded => LastResetDate != DateTime.UtcNow.Date;

    /// <summary>
    ///     Checks if a new hour has started since last reset.
    /// </summary>
    public bool IsHourlyResetNeeded =>
        LastHourReset != new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, DateTime.UtcNow.Day,
                                      DateTime.UtcNow.Hour, 0, 0, DateTimeKind.Utc);

    /// <summary>
    ///     Checks if a new minute has started since last reset.
    /// </summary>
    public bool IsMinuteResetNeeded
    {
        get
        {
            var now = DateTime.UtcNow;
            var thisMinute = new DateTime(now.Year, now.Month, now.Day, now.Hour, now.Minute, 0, DateTimeKind.Utc);
            return LastMinuteReset != thisMinute;
        }
    }
}
