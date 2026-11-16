using System.Text.Json;
using FluentAssertions;
using VanPattenAIRouter.Models;
using Xunit;

namespace VanPattenAIRouter.Tests.Services;

public class AgnesAiProviderConfigurationTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    [Theory]
    [InlineData("appsettings.json")]
    [InlineData("appsettings.Development.json")]
    public void AppSettings_ShouldContainAgnesProvider_WithExpectedOpenAiConfiguration(string fileName)
    {
        // Arrange
        var appSettingsPath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "..", fileName));
        var json = File.ReadAllText(appSettingsPath);
        var options = JsonSerializer.Deserialize<AppOptions>(json, JsonOptions);

        // Act
        var agnes = options?.Router.Providers.FirstOrDefault(p => p.Name == "Agnes AI");

        // Assert
        agnes.Should().NotBeNull();
        agnes!.BaseUrl.Should().Be("https://agnes-ai.com/v1");
        agnes.Type.Should().Be("openai");
        agnes.IsActive.Should().BeTrue();
        agnes.Priority.Should().Be(95);
        agnes.SupportsImages.Should().BeTrue();
        agnes.ImageTasksOnly.Should().BeTrue("Agnes AI must be restricted to image-related tasks only");
    }

    [Fact]
    public void DockerAppSettings_ShouldContainAgnesProvider_WithExpectedOpenAiConfiguration()
    {
        // Arrange
        var appSettingsPath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "..", "appsettings.Docker.json"));
        var json = File.ReadAllText(appSettingsPath);
        var options = JsonSerializer.Deserialize<AppOptions>(json, JsonOptions);

        // Act
        var agnes = options?.Router.Providers.FirstOrDefault(p => p.Name == "Agnes AI");

        // Assert
        agnes.Should().NotBeNull();
        agnes!.BaseUrl.Should().Be("https://agnes-ai.com/v1");
        agnes.Type.Should().Be("openai");
        agnes.IsActive.Should().BeTrue();
        agnes.Priority.Should().Be(95);
        agnes.SupportsImages.Should().BeTrue();
        agnes.ImageTasksOnly.Should().BeTrue("Agnes AI must be restricted to image-related tasks only");
    }
}
