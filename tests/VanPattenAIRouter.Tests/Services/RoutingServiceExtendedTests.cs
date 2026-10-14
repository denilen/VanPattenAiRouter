using System.Net;
using System.Text;
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

public class RoutingServiceExtendedTests : IDisposable
{
    private readonly AiRouterDbContext _context;
    private readonly List<HttpClient> _createdClients = new();
    private readonly ILogger<RoutingService> _logger;
    private readonly MockHttpMessageHandler _mockHttp;
    private readonly Mock<IProviderService> _mockProviderService;
    private readonly Mock<RetryPolicyService> _mockRetryPolicyService;
    private readonly RoutingService _service;

    public RoutingServiceExtendedTests()
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
        _logger = new LoggerFactory().CreateLogger<RoutingService>();

        var httpClientFactory = new Mock<IHttpClientFactory>();
        httpClientFactory
           .Setup(f => f.CreateClient(It.IsAny<string>()))
           .Returns(() =>
            {
                var client = _mockHttp.ToHttpClient();
                _createdClients.Add(client);
                return client;
            });

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
                                      _mockRetryPolicyService.Object, _logger);
    }

    public void Dispose()
    {
        _context.Dispose();
        foreach (var client in _createdClients) client.Dispose();
        _mockHttp.Dispose();
    }

    [Fact]
    public async Task RouteChatCompletionAsync_FailoverToSecondProvider_WhenFirstFails()
    {
        // Arrange
        var provider1 = new Provider
        {
            Id = 1,
            Name = "Provider 1",
            BaseUrl = "https://api.provider1.com/v1",
            ApiKey = "key1",
            Type = "openai",
            IsActive = true,
            Priority = 100,
            RateLimitJson = "{\"RequestsPerDay\": 1000, \"TokensPerDay\": 100000}",
            RequestsToday = 0,
            TokensToday = 0,
            LastResetDate = DateTime.UtcNow.Date
        };

        var provider2 = new Provider
        {
            Id = 2,
            Name = "Provider 2",
            BaseUrl = "https://api.provider2.com/v1",
            ApiKey = "key2",
            Type = "openai",
            IsActive = true,
            Priority = 50,
            RateLimitJson = "{\"RequestsPerDay\": 1000, \"TokensPerDay\": 100000}",
            RequestsToday = 0,
            TokensToday = 0,
            LastResetDate = DateTime.UtcNow.Date
        };

        // Mock ResolveAutoProviderAsync to return a non-virtual provider (triggers RouteToSingleProviderAsync)
        _mockProviderService.Setup(s => s.ResolveAutoProviderAsync("gpt-4o", It.IsAny<int>()))
                            .ReturnsAsync((provider1, "auto", (string?)null));

        // Mock GetAllAvailableProvidersForModelAsync to return both providers in priority order (for failover)
        _mockProviderService
           .Setup(s => s.GetAllAvailableProvidersForModelAsync("gpt-4o", It.IsAny<int>()))
           .ReturnsAsync(new List<Provider> { provider1, provider2 });

        // First provider returns 402 (failover)
        _mockHttp.When(HttpMethod.Post, "https://api.provider1.com/v1/chat/completions")
                 .Respond(HttpStatusCode.PaymentRequired, "application/json",
                          "{\"error\": {\"title\": \"Paid Model\", \"message\": \"Add credits\"}}");

        // Second provider succeeds
        var successResponse = new ChatCompletionResponse
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
                    Message = new ChatMessage { Role = "assistant", Content = "Success from provider 2" },
                    FinishReason = "stop"
                }
            },
            Usage = new Usage { PromptTokens = 10, CompletionTokens = 5, TotalTokens = 15 }
        };

        _mockHttp.When(HttpMethod.Post, "https://api.provider2.com/v1/chat/completions")
                 .Respond(HttpStatusCode.OK, "application/json", JsonSerializer.Serialize(successResponse));

        var request = new ChatCompletionRequest
        {
            Model = "gpt-4o",
            Messages = new List<ChatMessage> { new() { Role = "user", Content = "Hello" } }
        };

        // Act
        var result = await _service.RouteChatCompletionAsync(request, "test-key");

        // Assert
        result.Should().NotBeNull();
        result.Choices[0].Message.GetTextContent().Should().Be("Success from provider 2");
    }

    [Fact]
    public async Task RouteChatCompletionAsync_ReturnsError_WhenAllProvidersFail()
    {
        // Arrange
        var provider1 = new Provider
        {
            Id = 1,
            Name = "Provider 1",
            BaseUrl = "https://api.provider1.com/v1",
            ApiKey = "key1",
            Type = "openai",
            IsActive = true,
            Priority = 100,
            RateLimitJson = "{\"RequestsPerDay\": 1000, \"TokensPerDay\": 100000}",
            RequestsToday = 0,
            TokensToday = 0,
            LastResetDate = DateTime.UtcNow.Date
        };

        var provider2 = new Provider
        {
            Id = 2,
            Name = "Provider 2",
            BaseUrl = "https://api.provider2.com/v1",
            ApiKey = "key2",
            Type = "openai",
            IsActive = true,
            Priority = 50,
            RateLimitJson = "{\"RequestsPerDay\": 1000, \"TokensPerDay\": 100000}",
            RequestsToday = 0,
            TokensToday = 0,
            LastResetDate = DateTime.UtcNow.Date
        };

        // Mock ResolveAutoProviderAsync to return a non-virtual provider (triggers RouteToSingleProviderAsync)
        _mockProviderService.Setup(s => s.ResolveAutoProviderAsync("gpt-4o", It.IsAny<int>()))
                            .ReturnsAsync((provider1, "auto", (string?)null));

        _mockProviderService
           .Setup(s => s.GetAllAvailableProvidersForModelAsync("gpt-4o", It.IsAny<int>()))
           .ReturnsAsync(new List<Provider> { provider1, provider2 });

        // Both return 402
        _mockHttp.When(HttpMethod.Post, "https://api.provider1.com/v1/chat/completions")
                 .Respond(HttpStatusCode.PaymentRequired, "application/json",
                          "{\"error\": {\"title\": \"Paid Model\", \"message\": \"No credits\"}}");

        _mockHttp.When(HttpMethod.Post, "https://api.provider2.com/v1/chat/completions")
                 .Respond(HttpStatusCode.PaymentRequired, "application/json",
                          "{\"error\": {\"title\": \"Paid Model\", \"message\": \"No credits\"}}");

        var request = new ChatCompletionRequest
        {
            Model = "gpt-4o",
            Messages = new List<ChatMessage> { new() { Role = "user", Content = "Hello" } }
        };

        // Act
        var result = await _service.RouteChatCompletionAsync(request, "test-key");

        // Assert
        result.Should().NotBeNull();
        result.Choices.Should().NotBeEmpty();
        result.Choices[0].FinishReason.Should().Be("error");
        result.Choices[0].Message.GetTextContent().Should().Contain("All providers failed");
    }

    [Fact]
    public async Task RouteChatCompletionAsync_HandlesStreamingFailover()
    {
        // Arrange
        var provider1 = new Provider
        {
            Id = 1,
            Name = "Provider 1",
            BaseUrl = "https://api.provider1.com/v1",
            ApiKey = "key1",
            Type = "openai",
            IsActive = true,
            Priority = 100,
            RateLimitJson = "{\"RequestsPerDay\": 1000, \"TokensPerDay\": 100000}",
            RequestsToday = 0,
            TokensToday = 0,
            LastResetDate = DateTime.UtcNow.Date
        };

        var provider2 = new Provider
        {
            Id = 2,
            Name = "Provider 2",
            BaseUrl = "https://api.provider2.com/v1",
            ApiKey = "key2",
            Type = "openai",
            IsActive = true,
            Priority = 50,
            RateLimitJson = "{\"RequestsPerDay\": 1000, \"TokensPerDay\": 100000}",
            RequestsToday = 0,
            TokensToday = 0,
            LastResetDate = DateTime.UtcNow.Date
        };

        // Mock ResolveAutoProviderAsync to return a non-virtual provider (triggers RouteToSingleProviderAsync)
        _mockProviderService.Setup(s => s.ResolveAutoProviderAsync("gpt-4o", It.IsAny<int>()))
                            .ReturnsAsync((provider1, "auto", (string?)null));

        _mockProviderService
           .Setup(s => s.GetAllAvailableProvidersForModelAsync("gpt-4o", It.IsAny<int>()))
           .ReturnsAsync(new List<Provider> { provider1, provider2 });

        // First provider returns 402
        _mockHttp.When(HttpMethod.Post, "https://api.provider1.com/v1/chat/completions")
                 .Respond(HttpStatusCode.PaymentRequired, "application/json",
                          "{\"error\": {\"message\": \"No credits\"}}");

        // Second provider returns streaming response
        var streamResponse
            = "data: {\"id\":\"test\",\"object\":\"chat.completion.chunk\",\"created\":123,\"model\":\"gpt-4o\",\"choices\":[{\"index\":0,\"delta\":{\"content\":\"Hello\"},\"finish_reason\":null}]}\n\ndata: [DONE]\n\n";
        _mockHttp.When(HttpMethod.Post, "https://api.provider2.com/v1/chat/completions")
                 .Respond(HttpStatusCode.OK, "text/event-stream", streamResponse);

        var request = new ChatCompletionRequest
        {
            Model = "gpt-4o",
            Messages = new List<ChatMessage> { new() { Role = "user", Content = "Hello" } },
            Stream = true
        };

        // Act
        using var outputStream = new MemoryStream();
        await _service.RouteChatCompletionStreamAsync(request, "test-key", outputStream, CancellationToken.None);

        // Assert
        outputStream.Position = 0;
        var reader = new StreamReader(outputStream);
        var content = await reader.ReadToEndAsync();
        content.Should().Contain("data:");
    }

    [Fact]
    public async Task RouteChatCompletionAsync_Returns401ErrorResponse_WhenUnauthorized()
    {
        // Arrange
        var provider = new Provider
        {
            Id = 1,
            Name = "OpenAI",
            BaseUrl = "https://api.openai.com/v1",
            ApiKey = "invalid-key",
            Type = "openai",
            IsActive = true,
            Priority = 100,
            RateLimitJson = "{\"RequestsPerDay\": 1000, \"TokensPerDay\": 100000}",
            RequestsToday = 0,
            TokensToday = 0,
            LastResetDate = DateTime.UtcNow.Date
        };

        // Mock ResolveAutoProviderAsync to return a non-virtual provider (triggers RouteToSingleProviderAsync)
        _mockProviderService.Setup(s => s.ResolveAutoProviderAsync("gpt-4o", It.IsAny<int>()))
                            .ReturnsAsync((provider, "auto", (string?)null));

        _mockProviderService
           .Setup(s => s.GetAllAvailableProvidersForModelAsync("gpt-4o", It.IsAny<int>()))
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
        var result = await _service.RouteChatCompletionAsync(request, "test-key");

        // Assert
        result.Should().NotBeNull();
        result.Choices[0].FinishReason.Should().Be("error");
        result.Choices[0].Message.GetTextContent().Should().Contain("401");
    }

    [Fact]
    public async Task RouteChatCompletionAsync_Handles500ErrorWithRetry()
    {
        // Arrange
        var provider = new Provider
        {
            Id = 1,
            Name = "OpenAI",
            BaseUrl = "https://api.openai.com/v1",
            ApiKey = "key",
            Type = "openai",
            IsActive = true,
            Priority = 100,
            RateLimitJson = "{\"RequestsPerDay\": 1000, \"TokensPerDay\": 100000}",
            RequestsToday = 0,
            TokensToday = 0,
            LastResetDate = DateTime.UtcNow.Date
        };

        // Mock ResolveAutoProviderAsync to return a non-virtual provider (triggers RouteToSingleProviderAsync)
        _mockProviderService.Setup(s => s.ResolveAutoProviderAsync("gpt-4o", It.IsAny<int>()))
                            .ReturnsAsync((provider, "auto", (string?)null));

        _mockProviderService
           .Setup(s => s.GetAllAvailableProvidersForModelAsync("gpt-4o", It.IsAny<int>()))
           .ReturnsAsync(new List<Provider> { provider });

        // Override retry policy for this test to allow 1 retry
        _mockRetryPolicyService
           .Setup(r => r.GetRetryPolicy<ChatCompletionResponse>())
           .Returns(Policy<ChatCompletionResponse>
                   .Handle<Exception>()
                   .WaitAndRetryAsync(1, _ => TimeSpan.Zero, (_, _, _, _) => { }));

        // First attempt 500, second succeeds (mock retry policy with 1 retry)
        var attempt = 0;
        _mockHttp.When(HttpMethod.Post, "https://api.openai.com/v1/chat/completions")
                 .Respond(req =>
                  {
                      attempt++;
                      if (attempt == 1)
                          return new HttpResponseMessage(HttpStatusCode.InternalServerError)
                          {
                              Content = new StringContent("{\"error\": \"Server error\"}", Encoding.UTF8,
                                                          "application/json")
                          };
                      var success = new ChatCompletionResponse
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
                                  Message = new ChatMessage { Role = "assistant", Content = "Success after retry" },
                                  FinishReason = "stop"
                              }
                          },
                          Usage = new Usage { PromptTokens = 10, CompletionTokens = 5, TotalTokens = 15 }
                      };
                      return new HttpResponseMessage(HttpStatusCode.OK)
                      {
                          Content = new StringContent(JsonSerializer.Serialize(success), Encoding.UTF8,
                                                      "application/json")
                      };
                  });

        var request = new ChatCompletionRequest
        {
            Model = "gpt-4o",
            Messages = new List<ChatMessage> { new() { Role = "user", Content = "Hello" } }
        };

        // Act
        var result = await _service.RouteChatCompletionAsync(request, "test-key");

        // Assert - Should succeed after retry
        result.Should().NotBeNull();
        result.Choices[0].Message.GetTextContent().Should().Be("Success after retry");
    }

    [Fact]
    public async Task RouteChatCompletionAsync_RecordsUsage_WithCorrectTokens()
    {
        // Arrange
        var provider = new Provider
        {
            Id = 1,
            Name = "OpenAI",
            BaseUrl = "https://api.openai.com/v1",
            ApiKey = "key",
            Type = "openai",
            IsActive = true,
            Priority = 100,
            RateLimitJson = "{\"RequestsPerDay\": 1000, \"TokensPerDay\": 100000}",
            RequestsToday = 0,
            TokensToday = 0,
            LastResetDate = DateTime.UtcNow.Date
        };

        // Mock ResolveAutoProviderAsync to return a non-virtual provider (triggers RouteToSingleProviderAsync)
        _mockProviderService.Setup(s => s.ResolveAutoProviderAsync("gpt-4o", It.IsAny<int>()))
                            .ReturnsAsync((provider, "auto", (string?)null));

        _mockProviderService
           .Setup(s => s.GetAllAvailableProvidersForModelAsync("gpt-4o", It.IsAny<int>()))
           .ReturnsAsync(new List<Provider> { provider });

        var successResponse = new ChatCompletionResponse
        {
            Id = "chatcmpl-123",
            Object = "chat.completion",
            Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            Model = "gpt-4o",
            Choices = new List<ChatChoice>
            {
                new()
                {
                    Index = 0, Message = new ChatMessage { Role = "assistant", Content = "Response" },
                    FinishReason = "stop"
                }
            },
            Usage = new Usage { PromptTokens = 20, CompletionTokens = 30, TotalTokens = 50 }
        };

        _mockHttp.When(HttpMethod.Post, "https://api.openai.com/v1/chat/completions")
                 .Respond(HttpStatusCode.OK, "application/json", JsonSerializer.Serialize(successResponse));

        // Setup RecordUsageAsync verification
        _mockProviderService
           .Setup(s => s.RecordUsageAsync(1, It.IsAny<int>()))
           .Returns(Task.CompletedTask)
           .Verifiable();

        var request = new ChatCompletionRequest
        {
            Model = "gpt-4o",
            Messages = new List<ChatMessage> { new() { Role = "user", Content = "Hello" } }
        };

        // Act
        var result = await _service.RouteChatCompletionAsync(request, "test-key");

        // Assert
        result.Should().NotBeNull();
        _mockProviderService.Verify(s => s.RecordUsageAsync(1, 50), Times.Once);
    }
}