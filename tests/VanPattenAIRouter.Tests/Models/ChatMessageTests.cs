using System.Text.Json;
using FluentAssertions;
using VanPattenAIRouter.Models;
using Xunit;

namespace VanPattenAIRouter.Tests.Models;

public class ChatMessageTests
{
    [Fact]
    public void GetTextContent_ReturnsEmptyString_ForNullContent()
    {
        // Arrange
        var message = new ChatMessage { Role = "user", Content = null };

        // Act
        var result = message.GetTextContent();

        // Assert
        result.Should().Be("");
    }

    [Fact]
    public void GetTextContent_ReturnsString_ForStringContent()
    {
        // Arrange
        var message = new ChatMessage { Role = "user", Content = "Hello, world!" };

        // Act
        var result = message.GetTextContent();

        // Assert
        result.Should().Be("Hello, world!");
    }

    [Fact]
    public void GetTextContent_ExtractsText_FromMultimodalArray()
    {
        // Arrange
        var json = """
                   [
                       {"type": "text", "text": "Hello"},
                       {"type": "image_url", "image_url": {"url": "data:image/png;base64,abc"}},
                       {"type": "text", "text": "World"}
                   ]
                   """;
        var element = JsonSerializer.Deserialize<JsonElement>(json);
        var message = new ChatMessage { Role = "user", Content = element };

        // Act
        var result = message.GetTextContent();

        // Assert
        result.Should().Be("Hello World");
    }

    [Fact]
    public void GetTextContent_HandlesEmptyArray()
    {
        // Arrange
        var json = "[]";
        var element = JsonSerializer.Deserialize<JsonElement>(json);
        var message = new ChatMessage { Role = "user", Content = element };

        // Act
        var result = message.GetTextContent();

        // Assert
        result.Should().Be("");
    }

    [Fact]
    public void GetTextContent_HandlesArrayWithOnlyImages()
    {
        // Arrange
        var json = """
                   [
                       {"type": "image_url", "image_url": {"url": "data:image/png;base64,abc"}}
                   ]
                   """;
        var element = JsonSerializer.Deserialize<JsonElement>(json);
        var message = new ChatMessage { Role = "user", Content = element };

        // Act
        var result = message.GetTextContent();

        // Assert
        result.Should().Be("");
    }

    [Fact]
    public void GetTextContent_ReturnsToString_ForUnknownObject()
    {
        // Arrange
        var message = new ChatMessage { Role = "user", Content = 123 };

        // Act
        var result = message.GetTextContent();

        // Assert
        result.Should().Be("123");
    }

    [Fact]
    public void GetTextContent_HandlesJsonElementString()
    {
        // Arrange
        var element = JsonDocument.Parse("\"Just a string\"").RootElement;
        var message = new ChatMessage { Role = "user", Content = element };

        // Act
        var result = message.GetTextContent();

        // Assert
        result.Should().Be("Just a string");
    }
}