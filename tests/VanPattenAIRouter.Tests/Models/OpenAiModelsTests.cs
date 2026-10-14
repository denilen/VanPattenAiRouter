using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using VanPattenAIRouter.Models;
using Xunit;

namespace VanPattenAIRouter.Tests.Models;

public class OpenAiModelsTests
{
    [Fact]
    public void ChatCompletionRequest_DefaultValues_AreCorrect()
    {
        // Act
        var request = new ChatCompletionRequest();

        // Assert
        request.Model.Should().Be("");
        request.Messages.Should().NotBeNull().And.BeEmpty();
        request.Temperature.Should().BeNull();
        request.MaxTokens.Should().BeNull();
        request.TopP.Should().BeNull();
        request.N.Should().BeNull();
        request.Stream.Should().BeFalse();
        request.Stop.Should().BeNull();
        request.ResponseFormat.Should().BeNull();
        request.Tools.Should().BeNull();
        request.ToolChoice.Should().BeNull();
        request.Logprobs.Should().BeNull();
        request.TopLogprobs.Should().BeNull();
        request.User.Should().BeNull();
    }

    [Fact]
    public void ChatCompletionResponse_DefaultValues_AreCorrect()
    {
        // Act
        var response = new ChatCompletionResponse();

        // Assert
        response.Id.Should().NotBeEmpty();
        response.Object.Should().Be("chat.completion");
        response.Created.Should().BeCloseTo(DateTimeOffset.UtcNow.ToUnixTimeSeconds(), 5);
        response.Model.Should().Be("");
        response.Choices.Should().NotBeNull().And.BeEmpty();
        response.Usage.Should().NotBeNull();
        response.SystemFingerprint.Should().BeNull();
    }

    [Fact]
    public void ChatMessage_DefaultValues_AreCorrect()
    {
        // Act
        var message = new ChatMessage();

        // Assert
        message.Role.Should().Be("");
        message.Content.Should().Be("");
        message.Name.Should().BeNull();
        message.ToolCalls.Should().BeNull();
        message.ToolCallId.Should().BeNull();
    }

    [Fact]
    public void ChatToolCall_DefaultValues_AreCorrect()
    {
        // Act
        var toolCall = new ChatToolCall();

        // Assert
        toolCall.Id.Should().Be("");
        toolCall.Type.Should().Be("function");
        toolCall.Function.Should().NotBeNull();
        toolCall.Function.Name.Should().Be("");
        toolCall.Function.Arguments.Should().Be("");
    }

    [Fact]
    public void ChatChoice_DefaultValues_AreCorrect()
    {
        // Act
        var choice = new ChatChoice();

        // Assert
        choice.Index.Should().Be(0);
        choice.Message.Should().NotBeNull();
        choice.FinishReason.Should().Be("stop");
    }

    [Fact]
    public void Usage_DefaultValues_AreCorrect()
    {
        // Act
        var usage = new Usage();

        // Assert
        usage.PromptTokens.Should().Be(0);
        usage.CompletionTokens.Should().Be(0);
        usage.TotalTokens.Should().Be(0);
    }

    [Fact]
    public void EmbeddingRequest_DefaultValues_AreCorrect()
    {
        // Act
        var request = new EmbeddingRequest();

        // Assert
        request.Model.Should().Be("");
        request.Input.Should().NotBeNull().And.BeEmpty();
        request.EncodingFormat.Should().BeNull();
        request.Dimensions.Should().BeNull();
        request.User.Should().BeNull();
    }

    [Fact]
    public void EmbeddingResponse_DefaultValues_AreCorrect()
    {
        // Act
        var response = new EmbeddingResponse();

        // Assert
        response.Object.Should().Be("list");
        response.Data.Should().NotBeNull().And.BeEmpty();
        response.Model.Should().Be("");
        response.Usage.Should().NotBeNull();
    }

    [Fact]
    public void EmbeddingData_DefaultValues_AreCorrect()
    {
        // Act
        var data = new EmbeddingData();

        // Assert
        data.Object.Should().Be("embedding");
        data.Index.Should().Be(0);
        data.Embedding.Should().NotBeNull().And.BeEmpty();
    }

    [Fact]
    public void ModelInfo_DefaultValues_AreCorrect()
    {
        // Act
        var model = new ModelInfo();

        // Assert
        model.Id.Should().Be("");
        model.Object.Should().Be("model");
        model.Created.Should().BeCloseTo(DateTimeOffset.UtcNow.ToUnixTimeSeconds(), 5);
        model.OwnedBy.Should().Be("");
    }

    [Fact]
    public void ProviderDto_DefaultValues_AreCorrect()
    {
        // Act
        var dto = new ProviderDto();

        // Assert
        dto.Id.Should().Be(0);
        dto.Name.Should().Be("");
        dto.BaseUrl.Should().Be("");
        dto.ApiKey.Should().Be("");
        dto.Type.Should().Be("openai");
        dto.Headers.Should().BeNull();
        dto.IsActive.Should().BeTrue();
        dto.Priority.Should().Be(0);
        dto.Models.Should().NotBeNull().And.BeEmpty();
    }

    [Fact]
    public void ProviderModelDto_DefaultValues_AreCorrect()
    {
        // Act
        var dto = new ProviderModelDto();

        // Assert
        dto.Id.Should().Be(0);
        dto.ModelName.Should().Be("");
        dto.ProviderModelName.Should().BeNull();
        dto.IsEnabled.Should().BeTrue();
    }

    [Theory]
    [InlineData("gpt-4o", "user", "Hello", "assistant", "Hi there!")]
    public void ChatCompletionRequest_Serialization_WorksCorrectly(string model, string role1, string content1,
                                                                   string role2, string content2)
    {
        // Arrange
        var request = new ChatCompletionRequest
        {
            Model = model,
            Messages = new List<ChatMessage>
            {
                new() { Role = role1, Content = content1 },
                new() { Role = role2, Content = content2 }
            },
            Temperature = 0.7,
            MaxTokens = 100
        };

        // Act
        var json = JsonSerializer.Serialize(request, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        });

        // Assert
        json.Should().Contain($"\"model\":\"{model}\"");
        json.Should().Contain($"\"role\":\"{role1}\"");
        json.Should().Contain($"\"content\":\"{content1}\"");
        json.Should().Contain($"\"role\":\"{role2}\"");
        json.Should().Contain($"\"content\":\"{content2}\"");
        json.Should().Contain("\"temperature\":0.7");
        json.Should().Contain("\"max_tokens\":100");
    }

    [Fact]
    public void ChatCompletionRequest_WithNullOptionalFields_SerializesWithoutNulls()
    {
        // Arrange
        var request = new ChatCompletionRequest
        {
            Model = "gpt-4o",
            Messages = new List<ChatMessage> { new() { Role = "user", Content = "Hello" } },
            Stream = false // Explicitly set to false to test it's included
        };

        // Act
        var json = JsonSerializer.Serialize(request, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        });

        // Assert - non-nullable fields like Stream are always serialized
        json.Should().Contain("\"stream\":false");
        // Nullable fields should not be present
        json.Should().NotContain("temperature");
        json.Should().NotContain("max_tokens");
        json.Should().NotContain("top_p");
    }

    [Fact]
    public void EmbeddingRequest_Serialization_WorksCorrectly()
    {
        // Arrange
        var request = new EmbeddingRequest
        {
            Model = "text-embedding-3-small",
            Input = new List<string> { "Hello", "World" },
            EncodingFormat = "float",
            Dimensions = 512
        };

        // Act
        var json = JsonSerializer.Serialize(request, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        });

        // Assert
        json.Should().Contain("\"model\":\"text-embedding-3-small\"");
        json.Should().Contain("\"input\":[\"Hello\",\"World\"]");
        json.Should().Contain("\"encoding_format\":\"float\"");
        json.Should().Contain("\"dimensions\":512");
    }
}