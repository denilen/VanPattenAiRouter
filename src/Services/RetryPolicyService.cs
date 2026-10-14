using Microsoft.Extensions.Options;
using Polly;
using Polly.Retry;
using VanPattenAIRouter.Models;

namespace VanPattenAIRouter.Services;

/// <summary>
///     Service for managing retry policies with configurable backoff and retry conditions.
/// </summary>
public class RetryPolicyService
{
    private readonly RetryConfig _config;
    private readonly ILogger<RetryPolicyService> _logger;

    /// <summary>
    ///     Initializes a new instance of the <see cref="RetryPolicyService" /> class.
    /// </summary>
    /// <param name="config">The retry configuration.</param>
    /// <param name="logger">The logger.</param>
    public RetryPolicyService(IOptions<RetryConfig> config, ILogger<RetryPolicyService> logger)
    {
        _config = config.Value;
        _logger = logger;
    }

    /// <summary>
    ///     Creates a retry policy for the specified type.
    /// </summary>
    /// <typeparam name="T">The result type of the policy.</typeparam>
    /// <returns>An async retry policy configured with exponential backoff and logging.</returns>
    public virtual AsyncRetryPolicy<T> GetRetryPolicy<T>()
        where T : notnull
    {
        return Policy<T>
              .Handle<HttpRequestException>()
              .Or<TaskCanceledException>()
              .Or<TimeoutException>()
              .WaitAndRetryAsync(_config.MaxAttempts - 1,
                                 retryAttempt => GetDelay(retryAttempt),
                                 (outcome, timespan, retryCount, context) =>
                                 {
                                     var message = outcome.Exception?.Message ?? "Unknown error";
                                     _logger.LogWarning("Retry {RetryCount}/{MaxAttempts} after {Delay}ms: {Message}",
                                                        retryCount, _config.MaxAttempts, timespan.TotalMilliseconds,
                                                        message);
                                 });
    }

    /// <summary>
    ///     Calculates the delay for a given retry attempt.
    /// </summary>
    /// <param name="retryAttempt">The retry attempt number (1-based).</param>
    /// <returns>The delay time span.</returns>
    private TimeSpan GetDelay(int retryAttempt)
    {
        if (_config.ExponentialBackoff)
        {
            var delay = TimeSpan.FromSeconds(_config.BaseDelaySeconds * Math.Pow(2, retryAttempt - 1));
            return delay > TimeSpan.FromSeconds(_config.MaxDelaySeconds)
                ? TimeSpan.FromSeconds(_config.MaxDelaySeconds)
                : delay;
        }

        return TimeSpan.FromSeconds(_config.BaseDelaySeconds);
    }

    /// <summary>
    ///     Determines if an exception should trigger a retry.
    /// </summary>
    /// <param name="ex">The exception to check.</param>
    /// <returns>True if the exception is retryable, false otherwise.</returns>
    public bool IsRetryableException(Exception ex)
    {
        var exceptionType = ex.GetType().Name;

        // Check if it's a specific exception type that's always retryable
        if (_config.RetryableExceptions.Contains(exceptionType))
        {
            // For HttpRequestException, check if message contains a retryable status code
            if (exceptionType == nameof(HttpRequestException) && ex is HttpRequestException httpReqEx)
            {
                var message = httpReqEx.Message;
                if (string.IsNullOrEmpty(message) || IsDefaultExceptionMessage(message)) return false;

                return IsRetryableStatusCode(httpReqEx) || !HasStatusCodeInMessage(httpReqEx);
            }

            return true;
        }

        // Check HttpRequestException with status code (for non-configured cases)
        if (ex is HttpRequestException httpEx) return IsRetryableStatusCode(httpEx);

        return false;
    }

    /// <summary>
    ///     Checks if the exception message is a default/generic message without specific error details.
    /// </summary>
    /// <param name="message">The exception message.</param>
    /// <returns>True if the message appears to be a default exception message.</returns>
    private bool IsDefaultExceptionMessage(string message)
    {
        // Default HttpRequestException message pattern
        return message.Contains("was thrown") || message.Contains("Exception of type");
    }

    /// <summary>
    ///     Checks if the exception message contains any status code (retryable or non-retryable).
    /// </summary>
    /// <param name="ex">The HTTP request exception.</param>
    /// <returns>True if a status code is detected in the message.</returns>
    private bool HasStatusCodeInMessage(HttpRequestException ex)
    {
        var message = ex.Message;
        if (string.IsNullOrEmpty(message)) return false;

        foreach (var code in _config.RetryableStatusCodes)
            if (message.Contains(code.ToString()))
                return true;
        // Also check for common non-retryable status codes to detect if a status code is present
        var nonRetryableCodes = new[] { 400, 401, 403, 404 };
        foreach (var code in nonRetryableCodes)
            if (message.Contains(code.ToString()))
                return true;
        return false;
    }

    /// <summary>
    ///     Checks if the exception message contains a retryable HTTP status code.
    /// </summary>
    /// <param name="ex">The HTTP request exception.</param>
    /// <returns>True if a retryable status code is found in the message.</returns>
    private bool IsRetryableStatusCode(HttpRequestException ex)
    {
        var message = ex.Message;
        if (string.IsNullOrEmpty(message)) return false;

        foreach (var code in _config.RetryableStatusCodes)
            if (message.Contains(code.ToString()))
                return true;
        return false;
    }
}