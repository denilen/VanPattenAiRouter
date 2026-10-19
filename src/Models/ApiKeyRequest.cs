using System.Text.Json.Serialization;

namespace VanPattenAIRouter.Models;

/// <summary>
///     Represents an API key request.
/// </summary>
public class ApiKeyRequest
{
    /// <summary>
    ///     The name of the API key.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    /// <summary>
    ///     The role of the API key.
    /// </summary>
    [JsonPropertyName("role")]
    public string Role { get; set; } = "user";
}