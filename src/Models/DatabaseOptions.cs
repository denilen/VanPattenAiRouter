namespace VanPattenAIRouter.Models;

/// <summary>
///     Database configuration options.
/// </summary>
public class DatabaseOptions
{
    /// <summary>
    ///     Path to the SQLite database file.
    /// </summary>
    public string Path { get; set; } = "data/vanpatten-airrouter.db";

    /// <summary>
    ///     Whether to delete and recreate the database on startup.
    ///     Useful for development or when provider configuration has changed.
    /// </summary>
    public bool RecreateOnStartup { get; set; } = false;
}