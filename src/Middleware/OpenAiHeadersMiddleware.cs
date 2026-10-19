using System.Diagnostics;

namespace VanPattenAIRouter.Middleware;

/// <summary>
///     Middleware for adding OpenAI-compatible response headers.
/// </summary>
public class OpenAiHeadersMiddleware
{
    private readonly ILogger<OpenAiHeadersMiddleware> _logger;
    private readonly RequestDelegate _next;

    /// <summary>
    ///     Initializes a new instance of the <see cref="OpenAiHeadersMiddleware" /> class.
    /// </summary>
    /// <param name="next">The next middleware in the pipeline.</param>
    /// <param name="logger">The logger.</param>
    public OpenAiHeadersMiddleware(RequestDelegate next, ILogger<OpenAiHeadersMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    /// <summary>
    ///     Invokes the middleware.
    /// </summary>
    /// <param name="context">The HTTP context.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task InvokeAsync(HttpContext context)
    {
        // Generate or use existing request ID
        var requestId = context.Request.Headers.TryGetValue("x-request-id", out var requestIdValues)
            ? requestIdValues.FirstOrDefault() ?? GenerateRequestId()
            : GenerateRequestId();

        // Set response headers
        context.Response.OnStarting(() =>
        {
            // x-request-id: Always return the request ID
            context.Response.Headers["x-request-id"] = requestId;

            // openai-version: The version of the OpenAI API (use current date format)
            context.Response.Headers["openai-version"] = "2024-10-01-preview";

            // openai-organization: Organization ID (if configured, otherwise empty)
            // For now, we don't have org support, so we'll omit or set a default
            // context.Response.Headers["openai-organization"] = "org-default";

            // openai-processing-ms: Processing time in milliseconds
            // We'll calculate this from a stopwatch
            if (context.Items.TryGetValue("RequestStopwatch", out var stopwatchObj) && stopwatchObj is Stopwatch sw)
                context.Response.Headers["openai-processing-ms"] = sw.ElapsedMilliseconds.ToString();

            // Rate limit headers (x-ratelimit-*)
            // These would be populated by the routing service based on provider limits
            // For now, we'll add placeholder headers that can be overridden
            if (!context.Response.Headers.ContainsKey("x-ratelimit-limit"))
                context.Response.Headers["x-ratelimit-limit"] = "0";

            if (!context.Response.Headers.ContainsKey("x-ratelimit-remaining"))
                context.Response.Headers["x-ratelimit-remaining"] = "0";

            if (!context.Response.Headers.ContainsKey("x-ratelimit-reset"))
                context.Response.Headers["x-ratelimit-reset"] = "0";

            return Task.CompletedTask;
        });

        // Start stopwatch for processing time
        var stopwatch = Stopwatch.StartNew();
        context.Items["RequestStopwatch"] = stopwatch;

        try
        {
            await _next(context);
        }
        finally
        {
            stopwatch.Stop();
        }
    }

    /// <summary>
    ///     Generates a unique request ID in OpenAI format.
    /// </summary>
    /// <returns>A request ID string.</returns>
    private static string GenerateRequestId()
    {
        // OpenAI format: req_<random_string>
        return $"req_{Guid.NewGuid().ToString("N")[..24]}";
    }
}

/// <summary>
///     Extension methods for registering the OpenAI headers middleware.
/// </summary>
public static class OpenAiHeadersMiddlewareExtensions
{
    /// <summary>
    ///     Adds the OpenAI headers middleware to the pipeline.
    /// </summary>
    /// <param name="builder">The application builder.</param>
    /// <returns>The application builder for chaining.</returns>
    public static IApplicationBuilder UseOpenAiHeaders(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<OpenAiHeadersMiddleware>();
    }
}