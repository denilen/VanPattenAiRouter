using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using VanPattenAIRouter.Models;
using VanPattenAIRouter.Services;
using Xunit;

namespace VanPattenAIRouter.Tests.Unit;

/// <summary>
///     Unit tests for Phase 1: Chat Completions full OpenAI compatibility
/// </summary>
public class ChatCompletionTests
{
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    [Fact]
    public void ChatCompletionRequest_NewParameters_SerializeCorrectly()
    {
        // Arrange
        var request = new ChatCompletionRequest
        {
            Model = "gpt-4o",
            Messages = new List<ChatMessage>
            {
                new() { Role = "user", Content = "Hello" }
            },
            MaxCompletionTokens = 100,
            ParallelToolCalls = true,
            Store = true,
            StreamOptions = new ChatCompletionStreamOptions
            {
                IncludeUsage = true,
                IncludeObfuscation = false
            },
            ReasoningEffort = "high",
            ServiceTier = "priority",
            PromptCacheKey = "cache-key-123",
            Verbosity = "low",
            SafetyIdentifier = "user-123",
            Seed = 42,
            LogitBias = new Dictionary<int, double> { { 1234, 10.0 }, { 5678, -5.0 } },
            FrequencyPenalty = 0.5,
            PresencePenalty = -0.5,
            FunctionCall = new { name = "my_function" },
            Functions = new List<object> { new { name = "old_function", description = "Old style" } }
        };

        // Act
        var json = JsonSerializer.Serialize(request, _jsonOptions);

        // Assert
        json.Should().Contain("\"max_completion_tokens\":100");
        json.Should().Contain("\"parallel_tool_calls\":true");
        json.Should().Contain("\"store\":true");
        json.Should().Contain("\"stream_options\":{\"include_usage\":true,\"include_obfuscation\":false}");
        json.Should().Contain("\"reasoning_effort\":\"high\"");
        json.Should().Contain("\"service_tier\":\"priority\"");
        json.Should().Contain("\"prompt_cache_key\":\"cache-key-123\"");
        json.Should().Contain("\"verbosity\":\"low\"");
        json.Should().Contain("\"safety_identifier\":\"user-123\"");
        json.Should().Contain("\"seed\":42");
        json.Should().Contain("\"logit_bias\":{\"1234\":10,\"5678\":-5}");
        json.Should().Contain("\"frequency_penalty\":0.5");
        json.Should().Contain("\"presence_penalty\":-0.5");
        json.Should().Contain("\"function_call\":{\"name\":\"my_function\"}");
        json.Should().Contain("\"functions\":");
    }

    [Fact]
    public void ChatCompletionRequest_Prediction_SerializesCorrectly()
    {
        // Arrange
        var request = new ChatCompletionRequest
        {
            Model = "gpt-4o",
            Messages = new List<ChatMessage>
            {
                new() { Role = "user", Content = "Hello" }
            },
            Prediction = new ChatCompletionPrediction
            {
                Type = "content",
                Content = "Predicted content here"
            }
        };

        // Act
        var json = JsonSerializer.Serialize(request, _jsonOptions);

        // Assert
        json.Should().Contain("\"prediction\":{\"type\":\"content\",\"content\":\"Predicted content here\"}");
    }

    [Fact]
    public void ChatCompletionRequest_Tools_WithCustomTool_SerializesCorrectly()
    {
        // Arrange
        var request = new ChatCompletionRequest
        {
            Model = "gpt-4o",
            Messages = new List<ChatMessage>
            {
                new() { Role = "user", Content = "Hello" }
            },
            Tools = new List<Tool>
            {
                new()
                {
                    Type = "function",
                    Function = new FunctionDefinition
                    {
                        Name = "get_weather",
                        Description = "Get weather",
                        Parameters = JsonDocument.Parse("{\"type\":\"object\",\"properties\":{}}").RootElement
                    }
                },
                new()
                {
                    Type = "custom",
                    Custom = new CustomTool
                    {
                        Name = "my_custom_tool",
                        Description = "Custom tool",
                        Format = JsonDocument.Parse("{\"type\":\"text\"}").RootElement
                    }
                }
            }
        };

        // Act
        var json = JsonSerializer.Serialize(request, _jsonOptions);

        // Assert
        json.Should().Contain("\"type\":\"function\"");
        json.Should().Contain("\"name\":\"get_weather\"");
        json.Should().Contain("\"type\":\"custom\"");
        json.Should().Contain("\"name\":\"my_custom_tool\"");
    }

    [Fact]
    public void ChatCompletionResponse_SystemFingerprint_Included()
    {
        // Arrange
        var response = new ChatCompletionResponse
        {
            Id = "chatcmpl-123",
            Model = "gpt-4o",
            SystemFingerprint = "fp_abc123def456",
            ServiceTier = "priority",
            Choices = new List<ChatChoice>
            {
                new()
                {
                    Index = 0,
                    Message = new ChatMessage { Role = "assistant", Content = "Hello!" },
                    FinishReason = "stop",
                    Logprobs = new ChatLogprobs
                    {
                        Content = new List<TokenLogprob>
                        {
                            new() { Token = "Hello", Logprob = -0.1, Bytes = new List<int> { 72, 101, 108, 108, 111 } }
                        },
                        Refusal = new List<TokenLogprob>
                        {
                            new() { Token = "I'm sorry", Logprob = -5.0 }
                        }
                    }
                }
            },
            Usage = new Usage
            {
                PromptTokens = 10,
                CompletionTokens = 20,
                TotalTokens = 30,
                CompletionTokensDetails = new CompletionTokensDetails
                {
                    ReasoningTokens = 5,
                    AudioTokens = 0,
                    AcceptedPredictionTokens = 2,
                    RejectedPredictionTokens = 1
                }
            }
        };

        // Act
        var json = JsonSerializer.Serialize(response, _jsonOptions);

        // Assert
        json.Should().Contain("\"system_fingerprint\":\"fp_abc123def456\"");
        json.Should().Contain("\"service_tier\":\"priority\"");
        json.Should().Contain("\"logprobs\":{\"content\":");
        json.Should().Contain("\"refusal\":");
        json.Should()
            .Contain(
                 "\"completion_tokens_details\":{\"reasoning_tokens\":5,\"audio_tokens\":0,\"accepted_prediction_tokens\":2,\"rejected_prediction_tokens\":1}");
    }

    [Fact]
    public void ChatMessage_NewFields_SerializeCorrectly()
    {
        // Arrange
        var message = new ChatMessage
        {
            Role = "assistant",
            Content = "Response",
            Refusal = "I cannot do that",
            Audio = new ChatAudio
            {
                Id = "audio_123",
                Data = "base64data",
                ExpiresAt = 1234567890,
                Transcript = "Audio transcript"
            },
            Annotations = new List<ChatAnnotation>
            {
                new()
                {
                    Type = "url_citation",
                    UrlCitation = new UrlCitation
                    {
                        StartIndex = 0,
                        EndIndex = 10,
                        Title = "Example",
                        Url = "https://example.com"
                    }
                }
            }
        };

        // Act
        var json = JsonSerializer.Serialize(message, _jsonOptions);

        // Assert
        json.Should().Contain("\"refusal\":\"I cannot do that\"");
        json.Should()
            .Contain(
                 "\"audio\":{\"id\":\"audio_123\",\"data\":\"base64data\",\"expires_at\":1234567890,\"transcript\":\"Audio transcript\"}");
        json.Should()
            .Contain(
                 "\"annotations\":[{\"type\":\"url_citation\",\"url_citation\":{\"start_index\":0,\"end_index\":10,\"title\":\"Example\",\"url\":\"https://example.com\"}}");
    }

    [Fact]
    public void ChatCompletionStreamOptions_IncludeUsage_SerializesCorrectly()
    {
        // Arrange
        var options = new ChatCompletionStreamOptions
        {
            IncludeUsage = true,
            IncludeObfuscation = true
        };

        // Act
        var json = JsonSerializer.Serialize(options, _jsonOptions);

        // Assert
        json.Should().Be("{\"include_usage\":true,\"include_obfuscation\":true}");
    }

    [Fact]
    public void ChatCompletionPrediction_SerializesCorrectly()
    {
        // Arrange
        var prediction = new ChatCompletionPrediction
        {
            Type = "content",
            Content = "Predicted text"
        };

        // Act
        var json = JsonSerializer.Serialize(prediction, _jsonOptions);

        // Assert
        json.Should().Be("{\"type\":\"content\",\"content\":\"Predicted text\"}");
    }

    [Fact]
    public void CustomTool_SerializesCorrectly()
    {
        // Arrange
        var tool = new Tool
        {
            Type = "custom",
            Custom = new CustomTool
            {
                Name = "my_tool",
                Description = "A custom tool",
                Format = JsonDocument.Parse("{\"type\":\"text\"}").RootElement
            }
        };

        // Act
        var json = JsonSerializer.Serialize(tool, _jsonOptions);

        // Assert
        json.Should().Contain("\"type\":\"custom\"");
        json.Should().Contain("\"name\":\"my_tool\"");
        json.Should().Contain("\"description\":\"A custom tool\"");
        json.Should().Contain("\"format\":{\"type\":\"text\"}");
    }

    [Fact]
    public void GenerateSystemFingerprint_IsDeterministic()
    {
        // Act
        var fp1 = RoutingService.GenerateSystemFingerprint("test-provider", "test-model");
        var fp2 = RoutingService.GenerateSystemFingerprint("test-provider", "test-model");

        // Assert
        fp1.Should().Be(fp2);
        fp1.Should().StartWith("fp_");
        fp1.Length.Should().Be(15); // "fp_" + 12 hex chars
    }

    [Fact]
    public void GenerateSystemFingerprint_DifferentInputs_DifferentOutputs()
    {
        // Act
        var fp1 = RoutingService.GenerateSystemFingerprint("provider1", "model1");
        var fp2 = RoutingService.GenerateSystemFingerprint("provider2", "model1");
        var fp3 = RoutingService.GenerateSystemFingerprint("provider1", "model2");

        // Assert
        fp1.Should().NotBe(fp2);
        fp1.Should().NotBe(fp3);
        fp2.Should().NotBe(fp3);
    }
}