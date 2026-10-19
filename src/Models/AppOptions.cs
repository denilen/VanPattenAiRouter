namespace VanPattenAIRouter.Models;

/// <summary>
///     Application configuration options.
/// </summary>
public class AppOptions
{
    /// <summary>
    ///     Whether to use in-memory database (for testing).
    /// </summary>
    public bool UseInMemoryDatabase { get; set; }

    /// <summary>
    ///     Database configuration.
    /// </summary>
    public DatabaseOptions Database { get; set; } = new();

    /// <summary>
    ///     Router configuration.
    /// </summary>
    public RouterOptions Router { get; set; } = new();
}