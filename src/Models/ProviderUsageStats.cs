namespace VanPattenAIRouter.Models;

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