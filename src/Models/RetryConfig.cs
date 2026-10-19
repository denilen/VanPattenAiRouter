namespace VanPattenAIRouter.Models;

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