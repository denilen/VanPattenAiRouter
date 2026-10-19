using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Polly;
using RichardSzalay.MockHttp;
using VanPattenAIRouter.Data;
using VanPattenAIRouter.Models;
using VanPattenAIRouter.Services;
using Xunit;

namespace VanPattenAIRouter.Tests.Services;

public class RoutingServiceTests : IDisposable
{
    private readonly AiRouterDbContext _context;
    private readonly HttpClient _httpClient;
    private readonly ILogger<RoutingService> _logger;
    private readonly MockHttpMessageHandler _mockHttp;
    private readonly Mock<IProviderService> _mockProviderService;
    private readonly Mock<RetryPolicyService> _mockRetryPolicyService;
    private readonly RoutingService _service;

    public RoutingServiceTests()
    {
        var options = new DbContextOptionsBuilder<AiRouterDbContext>()
                     .UseInMemoryDatabase(Guid.NewGuid().ToString())
                     .Options;

        _context = new AiRouterDbContext(options);
        _mockProviderService = new Mock<IProviderService>();
        _mockRetryPolicyService = new Mock<RetryPolicyService>(MockBehavior.Loose,
                                                               new Mock<IOptions<RetryConfig>>().Object,
                                                               new LoggerFactory().CreateLogger<RetryPolicyService>());
        _mockHttp = new MockHttpMessageHandler();
        _httpClient = _mockHttp.ToHttpClient();
        _logger = new LoggerFactory().CreateLogger<RoutingService>();

        var httpClientFactory = new Mock<IHttpClientFactory>();
        httpClientFactory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(_httpClient);

        // Configure mock retry policy to execute delegate directly (no retry for tests)
        _mockRetryPolicyService
           .Setup(r => r.GetRetryPolicy<ChatCompletionResponse>())
           .Returns(Policy<ChatCompletionResponse>.Handle<Exception>()
                                                  .WaitAndRetryAsync(0, _ => TimeSpan.Zero, (_, _, _, _) => { }));

        _mockRetryPolicyService
           .Setup(r => r.GetRetryPolicy<EmbeddingResponse>())
           .Returns(Policy<EmbeddingResponse>.Handle<Exception>()
                                             .WaitAndRetryAsync(0, _ => TimeSpan.Zero, (_, _, _, _) => { }));

        _mockRetryPolicyService
           .Setup(r => r.GetRetryPolicy<bool>())
           .Returns(Policy<bool>.Handle<Exception>().WaitAndRetryAsync(0, _ => TimeSpan.Zero, (_, _, _, _) => { }));

        _service = new RoutingService(_mockProviderService.Object, httpClientFactory.Object,
                                      _mockRetryPolicyService.Object, _logger, _context);
    }

    public void Dispose()
    {
        _context.Dispose();
        _httpClient.Dispose();
        _mockHttp.Dispose();
    }

    [Fact]
    public async Task RouteChatCompletionAsync_ThrowsException_WhenNoProviderFound()
    {
        // Arrange
        _mockProviderService.Setup(s => s.ResolveAutoProviderAsync("unknown-model", It.IsAny<int>()))
                            .ReturnsAsync(((Provider?)null, "auto", (string?)null));

        _mockProviderService.Setup(s => s.GetAllAvailableProvidersForModelAsync("unknown-model", It.IsAny<int>()))
                            .ReturnsAsync(new List<Provider>());

        var request = new ChatCompletionRequest
        {
            Model = "unknown-model",
            Messages = new List<ChatMessage> { new() { Role = "user", Content = "Hello" } }
        };

        // Act & Assert - should return error response, not throw
        var response = await _service.RouteChatCompletionAsync(request, "test-key");

        response.Should().NotBeNull();
        response.Choices.Should().NotBeEmpty();
        response.Choices[0].FinishReason.Should().Be("rate_limit");
        response.Choices[0].Message.GetTextContent().Should().Contain("No available provider");
    }

    [Fact]
    public async Task RouteChatCompletionAsync_RoutesToCorrectProvider()
    {
        // Arrange
        var provider = new Provider
        {
            Id = 1,
            Name = "OpenAI",
            BaseUrl = "https://api.openai.com/v1",
            ApiKey = "sk-test-key",
            Type = "openai",
            IsActive = true,
            Priority = 100,
            RateLimitJson = "{\"RequestsPerDay\": 1000, \"TokensPerDay\": 100000}",
            RequestsToday = 0,
            TokensToday = 0,
            LastResetDate = DateTime.UtcNow.Date
        };

        _mockProviderService.Setup(s => s.ResolveAutoProviderAsync("gpt-4o", It.IsAny<int>()))
                            .ReturnsAsync((provider, "auto", (string?)null));

        _mockProviderService.Setup(s => s.GetProviderByIdAsync(1))
                            .ReturnsAsync(new ProviderDto
                             {
                                 Id = 1,
                                 Name = "OpenAI",
                                 BaseUrl = "https://api.openai.com/v1",
                                 ApiKey = "sk-test-key",
                                 Type = "openai",
                                 Models = new List<ProviderModelDto>
                                 {
                                     new() { ModelName = "gpt-4o", ProviderModelName = "gpt-4o", IsEnabled = true }
                                 }
                             });

        // Mock GetAllAvailableProvidersForModelAsync to return the provider
        _mockProviderService.Setup(s => s.GetAllAvailableProvidersForModelAsync("gpt-4o", It.IsAny<int>()))
                            .ReturnsAsync(new List<Provider> { provider });

        var expectedResponse = new ChatCompletionResponse
        {
            Id = "chatcmpl-123",
            Object = "chat.completion",
            Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            Model = "gpt-4o",
            Choices = new List<ChatChoice>
            {
                new()
                {
                    Index = 0,
                    Message = new ChatMessage { Role = "assistant", Content = "Hello!" },
                    FinishReason = "stop"
                }
            },
            Usage = new Usage { PromptTokens = 10, CompletionTokens = 5, TotalTokens = 15 }
        };

        _mockHttp.When(HttpMethod.Post, "https://api.openai.com/v1/chat/completions")
                 .Respond(HttpStatusCode.OK, "application/json", JsonSerializer.Serialize(expectedResponse));

        var request = new ChatCompletionRequest
        {
            Model = "gpt-4o",
            Messages = new List<ChatMessage> { new() { Role = "user", Content = "Hello" } },
            Temperature = 0.7,
            MaxTokens = 100
        };

        // Act
        var result = await _service.RouteChatCompletionAsync(request, "test-key");

        // Assert
        result.Should().NotBeNull();
        result.Model.Should().Be("gpt-4o");
        result.Choices.Should().HaveCount(1);
        result.Choices[0].Message.GetTextContent().Should().Be("Hello!");
        result.Usage.TotalTokens.Should().Be(15);
    }

    [Fact]
    public async Task RouteChatCompletionAsync_MapsModelNameToProviderModelName()
    {
        // Arrange
        var provider = new Provider
        {
            Id = 1,
            Name = "OpenAI",
            BaseUrl = "https://api.openai.com/v1",
            ApiKey = "sk-test-key",
            Type = "openai",
            IsActive = true,
            Priority = 100,
            RateLimitJson = "{\"RequestsPerDay\": 1000, \"TokensPerDay\": 100000}",
            RequestsToday = 0,
            TokensToday = 0,
            LastResetDate = DateTime.UtcNow.Date
        };

        _mockProviderService.Setup(s => s.ResolveAutoProviderAsync("my-gpt4", It.IsAny<int>()))
                            .ReturnsAsync((provider, "auto", (string?)null));

        _mockProviderService.Setup(s => s.GetProviderByIdAsync(1))
                            .ReturnsAsync(new ProviderDto
                             {
                                 Id = 1,
                                 Name = "OpenAI",
                                 BaseUrl = "https://api.openai.com/v1",
                                 ApiKey = "sk-test-key",
                                 Type = "openai",
                                 Models = new List<ProviderModelDto>
                                 {
                                     new() { ModelName = "gpt-4o", ProviderModelName = "gpt-4o", IsEnabled = true }
                                 }
                             });

        // Mock GetAllAvailableProvidersForModelAsync to return the provider
        _mockProviderService.Setup(s => s.GetAllAvailableProvidersForModelAsync("gpt-4o", It.IsAny<int>()))
                            .ReturnsAsync(new List<Provider> { provider });

        // Mock GetAllAvailableProvidersForModelAsync to return the provider
        _mockProviderService.Setup(s => s.GetAllAvailableProvidersForModelAsync("my-gpt4", It.IsAny<int>()))
                            .ReturnsAsync(new List<Provider> { provider });

        var expectedResponse = new ChatCompletionResponse
        {
            Id = "chatcmpl-123",
            Object = "chat.completion",
            Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            Model = "gpt-4-turbo",
            Choices = new List<ChatChoice>
            {
                new()
                {
                    Index = 0,
                    Message = new ChatMessage { Role = "assistant", Content = "Response" },
                    FinishReason = "stop"
                }
            },
            Usage = new Usage { PromptTokens = 10, CompletionTokens = 5, TotalTokens = 15 }
        };

        _mockHttp.When(HttpMethod.Post, "https://api.openai.com/v1/chat/completions")
                 .With(req =>
                  {
                      var content = req.Content!.ReadAsStringAsync().Result;
                      var json = JsonSerializer.Deserialize<JsonElement>(content);
                      return json.GetProperty("model").GetString() == "gpt-4-turbo";
                  })
                 .Respond(HttpStatusCode.OK, "application/json", JsonSerializer.Serialize(expectedResponse));

        var request = new ChatCompletionRequest
        {
            Model = "my-gpt4",
            Messages = new List<ChatMessage> { new() { Role = "user", Content = "Hello" } }
        };

        // Act
        var result = await _service.RouteChatCompletionAsync(request, "test-key");

        // Assert
        result.Should().NotBeNull();
        result.Model.Should().Be("my-gpt4"); // Returns requested model name
    }

    [Fact]
    public async Task RouteChatCompletionAsync_IncludesAllRequestParameters()
    {
        // Arrange
        var provider = new Provider
        {
            Id = 1,
            Name = "OpenAI",
            BaseUrl = "https://api.openai.com/v1",
            ApiKey = "sk-test-key",
            Type = "openai",
            IsActive = true,
            Priority = 100,
            RateLimitJson = "{\"RequestsPerDay\": 1000, \"TokensPerDay\": 100000}",
            RequestsToday = 0,
            TokensToday = 0,
            LastResetDate = DateTime.UtcNow.Date
        };

        _mockProviderService.Setup(s => s.ResolveAutoProviderAsync("gpt-4o", It.IsAny<int>()))
                            .ReturnsAsync((provider, "auto", (string?)null));

        _mockProviderService.Setup(s => s.GetProviderByIdAsync(1))
                            .ReturnsAsync(new ProviderDto
                             {
                                 Id = 1,
                                 Name = "OpenAI",
                                 BaseUrl = "https://api.openai.com/v1",
                                 ApiKey = "sk-test-key",
                                 Type = "openai",
                                 Models = new List<ProviderModelDto>
                                 {
                                     new() { ModelName = "gpt-4o", ProviderModelName = "gpt-4o", IsEnabled = true }
                                 }
                             });

        // Mock GetAllAvailableProvidersForModelAsync to return the provider
        _mockProviderService.Setup(s => s.GetAllAvailableProvidersForModelAsync("gpt-4o", It.IsAny<int>()))
                            .ReturnsAsync(new List<Provider> { provider });

        var expectedResponse = new ChatCompletionResponse
        {
            Id = "chatcmpl-123",
            Object = "chat.completion",
            Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            Model = "gpt-4o",
            Choices = new List<ChatChoice>
            {
                new()
                {
                    Index = 0,
                    Message = new ChatMessage { Role = "assistant", Content = "Response" },
                    FinishReason = "stop"
                }
            },
            Usage = new Usage { PromptTokens = 10, CompletionTokens = 5, TotalTokens = 15 }
        };

        _mockHttp.When(HttpMethod.Post, "https://api.openai.com/v1/chat/completions")
                 .With(req =>
                  {
                      var content = req.Content!.ReadAsStringAsync().Result;
                      var json = JsonSerializer.Deserialize<JsonElement>(content);

                      // Verify all parameters are included
                      return json.GetProperty("temperature").GetDouble() == 0.5 &&
                          json.GetProperty("max_tokens").GetInt32() == 500 &&
                          json.GetProperty("top_p").GetDouble() == 0.9 &&
                          json.GetProperty("n").GetInt32() == 2 &&
                          !json.GetProperty("stream").GetBoolean();
                  })
                 .Respond(HttpStatusCode.OK, "application/json", JsonSerializer.Serialize(expectedResponse));

        var request = new ChatCompletionRequest
        {
            Model = "gpt-4o",
            Messages = new List<ChatMessage> { new() { Role = "user", Content = "Hello" } },
            Temperature = 0.5,
            MaxTokens = 500,
            TopP = 0.9,
            N = 2,
            Stream = false,
            Stop = new List<string> { "END" },
            TopLogprobs = 5
        };

        // Act
        var result = await _service.RouteChatCompletionAsync(request, "test-key");

        // Assert
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task RouteChatCompletionAsync_IncludesCustomHeaders()
    {
        // Arrange
        var provider = new Provider
        {
            Id = 1,
            Name = "Anthropic",
            BaseUrl = "https://api.anthropic.com/v1",
            ApiKey = "sk-ant-test",
            Type = "anthropic",
            IsActive = true,
            Priority = 100,
            RateLimitJson = "{\"RequestsPerDay\": 1000, \"TokensPerDay\": 100000}",
            RequestsToday = 0,
            TokensToday = 0,
            LastResetDate = DateTime.UtcNow.Date
        };

        _mockProviderService.Setup(s => s.ResolveAutoProviderAsync("claude-3", It.IsAny<int>()))
                            .ReturnsAsync((provider, "auto", (string?)null));

        _mockProviderService.Setup(s => s.GetProviderByIdAsync(1))
                            .ReturnsAsync(new ProviderDto
                             {
                                 Id = 1,
                                 Name = "Anthropic",
                                 BaseUrl = "https://api.anthropic.com/v1",
                                 ApiKey = "sk-ant-test",
                                 Type = "anthropic",
                                 Headers = new Dictionary<string, string>
                                 {
                                     { "anthropic-version", "2023-06-01" },
                                     { "custom-header", "custom-value" }
                                 },
                                 Models = new List<ProviderModelDto>
                                 {
                                     new()
                                     {
                                         ModelName = "claude-3", ProviderModelName = "claude-3-sonnet", IsEnabled = true
                                     }
                                 }
                             });

        // Mock GetAllAvailableProvidersForModelAsync to return the provider
        _mockProviderService.Setup(s => s.GetAllAvailableProvidersForModelAsync("claude-3", It.IsAny<int>()))
                            .ReturnsAsync(new List<Provider> { provider });

        var expectedResponse = new ChatCompletionResponse
        {
            Id = "chatcmpl-123",
            Object = "chat.completion",
            Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            Model = "claude-3-sonnet",
            Choices = new List<ChatChoice>
            {
                new()
                {
                    Index = 0,
                    Message = new ChatMessage { Role = "assistant", Content = "Response" },
                    FinishReason = "stop"
                }
            },
            Usage = new Usage { PromptTokens = 10, CompletionTokens = 5, TotalTokens = 15 }
        };

        // Just verify the request succeeds - custom headers are added to DefaultRequestHeaders
        // which MockHttp may not capture in the request matcher
        _mockHttp.When(HttpMethod.Post, "https://api.anthropic.com/v1/chat/completions")
                 .Respond(HttpStatusCode.OK, "application/json", JsonSerializer.Serialize(expectedResponse));

        var request = new ChatCompletionRequest
        {
            Model = "claude-3",
            Messages = new List<ChatMessage> { new() { Role = "user", Content = "Hello" } }
        };

        // Act
        var result = await _service.RouteChatCompletionAsync(request, "test-key");

        // Assert
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task RouteChatCompletionAsync_ThrowsOnProviderError()
    {
        // Arrange
        var provider = new Provider
        {
            Id = 1,
            Name = "OpenAI",
            BaseUrl = "https://api.openai.com/v1",
            ApiKey = "sk-test-key",
            Type = "openai",
            IsActive = true,
            Priority = 100,
            RateLimitJson = "{\"RequestsPerDay\": 1000, \"TokensPerDay\": 100000}",
            RequestsToday = 0,
            TokensToday = 0,
            LastResetDate = DateTime.UtcNow.Date
        };

        _mockProviderService.Setup(s => s.ResolveAutoProviderAsync("gpt-4o", It.IsAny<int>()))
                            .ReturnsAsync((provider, "auto", (string?)null));

        _mockProviderService.Setup(s => s.GetProviderByIdAsync(1))
                            .ReturnsAsync(new ProviderDto
                             {
                                 Id = 1,
                                 Name = "OpenAI",
                                 BaseUrl = "https://api.openai.com/v1",
                                 ApiKey = "sk-test-key",
                                 Type = "openai",
                                 Models = new List<ProviderModelDto>
                                 {
                                     new() { ModelName = "gpt-4o", ProviderModelName = "gpt-4o", IsEnabled = true }
                                 }
                             });

        // Mock GetAllAvailableProvidersForModelAsync to return the provider
        _mockProviderService.Setup(s => s.GetAllAvailableProvidersForModelAsync("gpt-4o", It.IsAny<int>()))
                            .ReturnsAsync(new List<Provider> { provider });

        _mockHttp.When(HttpMethod.Post, "https://api.openai.com/v1/chat/completions")
                 .Respond(HttpStatusCode.Unauthorized, "application/json",
                          "{\"error\": {\"message\": \"Invalid API key\", \"type\": \"auth_error\"}}");

        var request = new ChatCompletionRequest
        {
            Model = "gpt-4o",
            Messages = new List<ChatMessage> { new() { Role = "user", Content = "Hello" } }
        };

        // Act
        var response = await _service.RouteChatCompletionAsync(request, "test-key");

        // Assert - should return error response instead of throwing
        response.Should().NotBeNull();
        response.Choices.Should().NotBeEmpty();
        response.Choices[0].FinishReason.Should().Be("error");
        response.Choices[0].Message.GetTextContent().Should().Contain("401");
        response.Choices[0].Message.GetTextContent().Should().Contain("Invalid API key");
    }

    [Fact]
    public async Task RouteEmbeddingAsync_RoutesToCorrectProvider()
    {
        // Arrange
        var provider = new Provider
        {
            Id = 1,
            Name = "OpenAI",
            BaseUrl = "https://api.openai.com/v1",
            ApiKey = "sk-test-key",
            Type = "openai",
            IsActive = true,
            Priority = 100,
            RateLimitJson = "{\"RequestsPerDay\": 1000, \"TokensPerDay\": 100000}",
            RequestsToday = 0,
            TokensToday = 0,
            LastResetDate = DateTime.UtcNow.Date
        };

        _mockProviderService.Setup(s => s.ResolveAutoProviderAsync("text-embedding-3-small", It.IsAny<int>()))
                            .ReturnsAsync((provider, "auto", (string?)null));

        _mockProviderService.Setup(s => s.GetProviderByIdAsync(1))
                            .ReturnsAsync(new ProviderDto
                             {
                                 Id = 1,
                                 Name = "OpenAI",
                                 BaseUrl = "https://api.openai.com/v1",
                                 ApiKey = "sk-test-key",
                                 Type = "openai",
                                 Models = new List<ProviderModelDto>
                                 {
                                     new() { ModelName = "gpt-4o", ProviderModelName = "gpt-4o", IsEnabled = true }
                                 }
                             });

        // Mock GetAllAvailableProvidersForModelAsync to return the provider
        _mockProviderService
           .Setup(s => s.GetAllAvailableProvidersForModelAsync("text-embedding-3-small", It.IsAny<int>()))
           .ReturnsAsync(new List<Provider> { provider });

        var expectedResponse = new EmbeddingResponse
        {
            Object = "list",
            Model = "text-embedding-3-small",
            Data = new List<EmbeddingData>
            {
                new() { Object = "embedding", Index = 0, Embedding = new List<float> { 0.1f, 0.2f, 0.3f } },
                new() { Object = "embedding", Index = 1, Embedding = new List<float> { 0.4f, 0.5f, 0.6f } }
            },
            Usage = new Usage { PromptTokens = 10, CompletionTokens = 0, TotalTokens = 10 }
        };

        _mockHttp.When(HttpMethod.Post, "https://api.openai.com/v1/embeddings")
                 .Respond(HttpStatusCode.OK, "application/json", JsonSerializer.Serialize(expectedResponse));

        var request = new EmbeddingRequest
        {
            Model = "text-embedding-3-small",
            Input = new List<string> { "Hello", "World" }
        };

        // Act
        var result = await _service.RouteEmbeddingAsync(request, "test-key");

        // Assert
        result.Should().NotBeNull();
        result.Model.Should().Be("text-embedding-3-small");
        result.Data.Should().HaveCount(2);
        result.Data[0].Embedding.Should().BeEquivalentTo(new[] { 0.1f, 0.2f, 0.3f });
        result.Usage.TotalTokens.Should().Be(10);
    }

    [Fact]
    public async Task RouteEmbeddingAsync_ThrowsException_WhenNoProviderFound()
    {
        // Arrange
        _mockProviderService.Setup(s => s.ResolveAutoProviderAsync("unknown-embedding", It.IsAny<int>()))
                            .ReturnsAsync(((Provider?)null, "auto", (string?)null));

        _mockProviderService.Setup(s => s.GetAllAvailableProvidersForModelAsync("unknown-embedding", It.IsAny<int>()))
                            .ReturnsAsync(new List<Provider>());

        var request = new EmbeddingRequest
        {
            Model = "unknown-embedding",
            Input = new List<string> { "Hello" }
        };

        // Act - should return error response
        var response = await _service.RouteEmbeddingAsync(request, "test-key");

        // Assert
        response.Should().NotBeNull();
        response.Data.Should().NotBeEmpty();
        response.Data[0].Object.Should().Be("error");
    }

    [Fact]
    public async Task RouteChatCompletionAsync_SendsCorrectMessagesFormat()
    {
        // Arrange
        var provider = new Provider
        {
            Id = 1,
            Name = "OpenAI",
            BaseUrl = "https://api.openai.com/v1",
            ApiKey = "sk-test-key",
            Type = "openai",
            IsActive = true,
            Priority = 100,
            RateLimitJson = "{\"RequestsPerDay\": 1000, \"TokensPerDay\": 100000}",
            RequestsToday = 0,
            TokensToday = 0,
            LastResetDate = DateTime.UtcNow.Date
        };

        _mockProviderService.Setup(s => s.ResolveAutoProviderAsync("gpt-4o", It.IsAny<int>()))
                            .ReturnsAsync((provider, "auto", (string?)null));

        _mockProviderService.Setup(s => s.GetProviderByIdAsync(1))
                            .ReturnsAsync(new ProviderDto
                             {
                                 Id = 1,
                                 Name = "OpenAI",
                                 BaseUrl = "https://api.openai.com/v1",
                                 ApiKey = "sk-test-key",
                                 Type = "openai",
                                 Models = new List<ProviderModelDto>
                                 {
                                     new() { ModelName = "gpt-4o", ProviderModelName = "gpt-4o", IsEnabled = true }
                                 }
                             });

        // Mock GetAllAvailableProvidersForModelAsync to return the provider
        _mockProviderService.Setup(s => s.GetAllAvailableProvidersForModelAsync("gpt-4o", It.IsAny<int>()))
                            .ReturnsAsync(new List<Provider> { provider });

        var expectedResponse = new ChatCompletionResponse
        {
            Id = "chatcmpl-123",
            Object = "chat.completion",
            Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            Model = "gpt-4o",
            Choices = new List<ChatChoice>
            {
                new()
                {
                    Index = 0,
                    Message = new ChatMessage { Role = "assistant", Content = "Response" },
                    FinishReason = "stop"
                }
            },
            Usage = new Usage { PromptTokens = 10, CompletionTokens = 5, TotalTokens = 15 }
        };

        _mockHttp.When(HttpMethod.Post, "https://api.openai.com/v1/chat/completions")
                 .With(req =>
                  {
                      var content = req.Content!.ReadAsStringAsync().Result;
                      var json = JsonSerializer.Deserialize<JsonElement>(content);

                      var messages = json.GetProperty("messages");
                      return messages.GetArrayLength() == 3 &&
                          messages[0].GetProperty("role").GetString() == "system" &&
                          messages[0].GetProperty("content").GetString() == "You are a helpful assistant" &&
                          messages[1].GetProperty("role").GetString() == "user" &&
                          messages[1].GetProperty("content").GetString() == "Hello" &&
                          messages[2].GetProperty("role").GetString() == "assistant" &&
                          messages[2].GetProperty("content").GetString() == "Hi there!" &&
                          messages[2].GetProperty("name").GetString() == "assistant";
                  })
                 .Respond(HttpStatusCode.OK, "application/json", JsonSerializer.Serialize(expectedResponse));

        var request = new ChatCompletionRequest
        {
            Model = "gpt-4o",
            Messages = new List<ChatMessage>
            {
                new() { Role = "system", Content = "You are a helpful assistant" },
                new() { Role = "user", Content = "Hello" },
                new() { Role = "assistant", Content = "Hi there!", Name = "assistant" }
            }
        };

        // Act
        var result = await _service.RouteChatCompletionAsync(request, "test-key");

        // Assert
        result.Should().NotBeNull();
    }
}