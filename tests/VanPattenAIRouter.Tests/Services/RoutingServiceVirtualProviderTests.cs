using System.Diagnostics;
using System.Net;
using System.Reflection;
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

public class RoutingServiceVirtualProviderTests : IDisposable
{
    private readonly AiRouterDbContext _context;
    private readonly HttpClient _httpClient;
    private readonly ILogger<RoutingService> _logger;
    private readonly MockHttpMessageHandler _mockHttp;
    private readonly Mock<IProviderService> _mockProviderService;
    private readonly Mock<RetryPolicyService> _mockRetryPolicyService;
    private readonly RoutingService _service;

    public RoutingServiceVirtualProviderTests()
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

        _mockRetryPolicyService
           .Setup(r => r.GetRetryPolicy<ChatCompletionResponse>())
           .Returns(Policy<ChatCompletionResponse>.Handle<Exception>()
                                                  .WaitAndRetryAsync(0, _ => TimeSpan.Zero, (_, _, _, _) => { }));

        _mockRetryPolicyService
           .Setup(r => r.GetRetryPolicy<bool>())
           .Returns(Policy<bool>.Handle<Exception>().WaitAndRetryAsync(0, _ => TimeSpan.Zero, (_, _, _, _) => { }));

        // Setup GetFreeModelsForAutoModeAsync for the virtual provider name used in tests
        _mockProviderService
           .Setup(s => s.GetFreeModelsForAutoModeAsync("VanPatten Router"))
           .ReturnsAsync(new List<ProviderModel>());

        _service = new RoutingService(_mockProviderService.Object, httpClientFactory.Object,
                                      _mockRetryPolicyService.Object, _logger);
    }

    public void Dispose()
    {
        _context.Dispose();
        _httpClient.Dispose();
        _mockHttp.Dispose();
    }

    [Fact]
    public async Task RouteVirtualProviderAsync_RoutesToAutoFreeProvider()
    {
        // Arrange
        var virtualProvider = new Provider
        {
            Id = 1,
            Name = "VanPatten Router",
            BaseUrl = "",
            ApiKey = "",
            Type = "virtual",
            IsActive = true,
            IsVirtual = true,
            Priority = 200,
            RateLimitJson = "{\"RequestsPerDay\": 10000, \"TokensPerDay\": 1000000}",
            RequestsToday = 0,
            TokensToday = 0,
            LastResetDate = DateTime.UtcNow.Date
        };

        var freeProvider = new Provider
        {
            Id = 2,
            Name = "Free Provider",
            BaseUrl = "https://api.free.com/v1",
            ApiKey = "free-key",
            Type = "openai",
            IsActive = true,
            IsVirtual = false,
            Priority = 100,
            RateLimitJson = "{\"RequestsPerDay\": 100, \"TokensPerDay\": 10000}",
            RequestsToday = 0,
            TokensToday = 0,
            LastResetDate = DateTime.UtcNow.Date
        };

        _mockProviderService
           .Setup(s => s.ResolveAutoProviderAsync("VanPatten/auto-free", It.IsAny<int>()))
           .ReturnsAsync((virtualProvider, "auto", (string?)null));

        _mockProviderService
           .Setup(s => s.GetAllAvailableProvidersForModelAsync("VanPatten/auto-free", It.IsAny<int>()))
           .ReturnsAsync(new List<Provider> { freeProvider });

        // Setup free models for auto-free mode
        var freeModels = new List<ProviderModel>
        {
            new()
            {
                Id = 1,
                ProviderId = freeProvider.Id,
                ModelName = "auto-free",
                IsEnabled = true,
                IsFree = true,
                Mode = "auto-free",
                Provider = freeProvider
            }
        };
        _mockProviderService
           .Setup(s => s.GetFreeModelsForAutoModeAsync("VanPatten Router"))
           .ReturnsAsync(freeModels);

        var successResponse = new ChatCompletionResponse
        {
            Id = "chatcmpl-123",
            Object = "chat.completion",
            Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            Model = "VanPatten/auto-free",
            Choices = new List<ChatChoice>
            {
                new()
                {
                    Index = 0,
                    Message = new ChatMessage { Role = "assistant", Content = "Auto free response" },
                    FinishReason = "stop"
                }
            },
            Usage = new Usage { PromptTokens = 10, CompletionTokens = 5, TotalTokens = 15 }
        };

        _mockHttp.When(HttpMethod.Post, "https://api.free.com/v1/chat/completions")
                 .Respond(HttpStatusCode.OK, "application/json", JsonSerializer.Serialize(successResponse));

        var request = new ChatCompletionRequest
        {
            Model = "VanPatten/auto-free",
            Messages = new List<ChatMessage> { new() { Role = "user", Content = "Hello" } }
        };

        // Act - Use reflection to call RouteVirtualProviderAsync
        var method = typeof(RoutingService).GetMethod("RouteVirtualProviderAsync",
                                                      BindingFlags.NonPublic | BindingFlags.Instance);
        var result = await (Task<ChatCompletionResponse>)method!.Invoke(
            _service,
            new object[] { virtualProvider, request, "test-key", 0, Stopwatch.StartNew(), "auto-free", null, CancellationToken.None })!;

        // Assert
        result.Should().NotBeNull();
        result.Choices[0].Message.GetTextContent().Should().Be("Auto free response");
    }

    [Fact]
    public async Task RouteVirtualProviderAsync_RoutesToAutoAllProvider()
    {
        // Arrange
        var virtualProvider = new Provider
        {
            Id = 1,
            Name = "VanPatten Router",
            BaseUrl = "",
            ApiKey = "",
            Type = "virtual",
            IsActive = true,
            IsVirtual = true,
            Priority = 200,
            RateLimitJson = "{\"RequestsPerDay\": 10000, \"TokensPerDay\": 1000000}",
            RequestsToday = 0,
            TokensToday = 0,
            LastResetDate = DateTime.UtcNow.Date
        };

        var highPriorityProvider = new Provider
        {
            Id = 2,
            Name = "High Priority",
            BaseUrl = "https://api.high.com/v1",
            ApiKey = "key",
            Type = "openai",
            IsActive = true,
            IsVirtual = false,
            Priority = 100,
            RateLimitJson = "{\"RequestsPerDay\": 100, \"TokensPerDay\": 10000}",
            RequestsToday = 0,
            TokensToday = 0,
            LastResetDate = DateTime.UtcNow.Date
        };

        _mockProviderService
           .Setup(s => s.ResolveAutoProviderAsync("VanPatten/auto-all", It.IsAny<int>()))
           .ReturnsAsync((virtualProvider, "auto", (string?)null));

        _mockProviderService
           .Setup(s => s.GetAllAvailableProvidersForModelAsync("VanPatten/auto-all", It.IsAny<int>()))
           .ReturnsAsync(new List<Provider> { highPriorityProvider });

        // Setup models for auto-all mode (mode="auto" uses GetAllModelsForAutoModeAsync)
        var freeModelsAll = new List<ProviderModel>
        {
            new()
            {
                Id = 2,
                ProviderId = highPriorityProvider.Id,
                ModelName = "auto-all",
                IsEnabled = true,
                IsFree = true,
                Mode = "auto-all",
                Provider = highPriorityProvider
            }
        };
        _mockProviderService
           .Setup(s => s.GetAllModelsForAutoModeAsync("VanPatten Router"))
           .ReturnsAsync(freeModelsAll);

        _mockProviderService
           .Setup(s => s.GetFreeModelsForAutoModeAsync("VanPatten Router"))
           .ReturnsAsync(freeModelsAll);

        var successResponse = new ChatCompletionResponse
        {
            Id = "chatcmpl-123",
            Object = "chat.completion",
            Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            Model = "VanPatten/auto-all",
            Choices = new List<ChatChoice>
            {
                new()
                {
                    Index = 0,
                    Message = new ChatMessage { Role = "assistant", Content = "Auto all response" },
                    FinishReason = "stop"
                }
            },
            Usage = new Usage { PromptTokens = 10, CompletionTokens = 5, TotalTokens = 15 }
        };

        _mockHttp.When(HttpMethod.Post, "https://api.high.com/v1/chat/completions")
                 .Respond(HttpStatusCode.OK, "application/json", JsonSerializer.Serialize(successResponse));

        var request = new ChatCompletionRequest
        {
            Model = "VanPatten/auto-all",
            Messages = new List<ChatMessage> { new() { Role = "user", Content = "Hello" } }
        };

        // Act
        var method = typeof(RoutingService).GetMethod("RouteVirtualProviderAsync",
                                                      BindingFlags.NonPublic | BindingFlags.Instance);
        var result = await (Task<ChatCompletionResponse>)method!.Invoke(
            _service,
            new object[] { virtualProvider, request, "test-key", 0, Stopwatch.StartNew(), "auto", null, CancellationToken.None })!;

        // Assert
        result.Should().NotBeNull();
        result.Choices[0].Message.GetTextContent().Should().Be("Auto all response");
    }

    [Fact]
    public async Task RouteVirtualProviderStreamAsync_StreamsFromProvider()
    {
        // Arrange
        var virtualProvider = new Provider
        {
            Id = 1,
            Name = "VanPatten Router",
            BaseUrl = "",
            ApiKey = "",
            Type = "virtual",
            IsActive = true,
            IsVirtual = true,
            Priority = 200
        };

        var streamProvider = new Provider
        {
            Id = 2,
            Name = "Stream Provider",
            BaseUrl = "https://api.stream.com/v1",
            ApiKey = "key",
            Type = "openai",
            IsActive = true,
            IsVirtual = false,
            Priority = 100
        };

        _mockProviderService
           .Setup(s => s.ResolveAutoProviderAsync("VanPatten/auto-free", It.IsAny<int>()))
           .ReturnsAsync((virtualProvider, "auto", (string?)null));

        _mockProviderService
           .Setup(s => s.GetAllAvailableProvidersForModelAsync("VanPatten/auto-free", It.IsAny<int>()))
           .ReturnsAsync(new List<Provider> { streamProvider });

        // Setup models for auto mode (mode="auto" uses GetAllModelsForAutoModeAsync)
        var streamModels = new List<ProviderModel>
        {
            new()
            {
                Id = 2,
                ProviderId = streamProvider.Id,
                ModelName = "auto-free",
                IsEnabled = true,
                IsFree = true,
                Mode = "auto",
                Provider = streamProvider
            }
        };
        _mockProviderService
           .Setup(s => s.GetAllModelsForAutoModeAsync("VanPatten Router"))
           .ReturnsAsync(streamModels);

        var streamResponse
            = "data: {\"id\":\"test\",\"object\":\"chat.completion.chunk\",\"created\":123,\"model\":\"gpt-4o\",\"choices\":[{\"index\":0,\"delta\":{\"content\":\"Hello\"},\"finish_reason\":null}]}\n\ndata: [DONE]\n\n";
        _mockHttp.When(HttpMethod.Post, "https://api.stream.com/v1/chat/completions")
                 .Respond(HttpStatusCode.OK, "text/event-stream", streamResponse);

        var request = new ChatCompletionRequest
        {
            Model = "VanPatten/auto-free",
            Messages = new List<ChatMessage> { new() { Role = "user", Content = "Hello" } },
            Stream = true
        };

        // Act
        using var outputStream = new MemoryStream();
        var method = typeof(RoutingService).GetMethod("RouteVirtualProviderStreamAsync",
                                                      BindingFlags.NonPublic | BindingFlags.Instance);
        await (Task)method!.Invoke(
            _service,
            new object[]
            {
                virtualProvider, request, "test-key", 0, outputStream, Stopwatch.StartNew(), "auto", null, CancellationToken.None
            })!;

        // Assert
        outputStream.Position = 0;
        var reader = new StreamReader(outputStream);
        var content = await reader.ReadToEndAsync();
        content.Should().Contain("data:");
    }

    [Fact]
    public async Task RouteVirtualProviderAsync_ReturnsError_WhenNoFreeProvidersAvailable()
    {
        // Arrange
        var virtualProvider = new Provider
        {
            Id = 1,
            Name = "VanPatten Router",
            BaseUrl = "",
            ApiKey = "",
            Type = "virtual",
            IsActive = true,
            IsVirtual = true,
            Priority = 200
        };

        // No available providers
        _mockProviderService
           .Setup(s => s.ResolveAutoProviderAsync("VanPatten/auto-free", It.IsAny<int>()))
           .ReturnsAsync((virtualProvider, "auto", (string?)null));

        _mockProviderService
           .Setup(s => s.GetAllAvailableProvidersForModelAsync("VanPatten/auto-free", It.IsAny<int>()))
           .ReturnsAsync(new List<Provider>());

        // Setup empty models for auto mode (mode="auto" uses GetAllModelsForAutoModeAsync)
        _mockProviderService
           .Setup(s => s.GetAllModelsForAutoModeAsync("VanPatten Router"))
           .ReturnsAsync(new List<ProviderModel>());

        // Setup empty free models for auto-free mode (to trigger the error)
        _mockProviderService
           .Setup(s => s.GetFreeModelsForAutoModeAsync("VanPatten Router"))
           .ReturnsAsync(new List<ProviderModel>());

        var request = new ChatCompletionRequest
        {
            Model = "VanPatten/auto-free",
            Messages = new List<ChatMessage> { new() { Role = "user", Content = "Hello" } }
        };

        // Act
        var method = typeof(RoutingService).GetMethod("RouteVirtualProviderAsync",
                                                      BindingFlags.NonPublic | BindingFlags.Instance);
        var result = await (Task<ChatCompletionResponse>)method!.Invoke(
            _service,
            new object[] { virtualProvider, request, "test-key", 0, Stopwatch.StartNew(), "auto-free", null, CancellationToken.None })!;

        // Assert
        result.Should().NotBeNull();
        result.Choices.Should().NotBeEmpty();
        result.Choices[0].FinishReason.Should().Be("rate_limit");
        result.Choices[0].Message.GetTextContent().Should().Contain("No models available for auto-free");
    }

    [Fact]
    public async Task RouteChatCompletionAsync_CallsVirtualProvider_WhenModelIsAutoFree()
    {
        // Arrange - Test the public method that routes to virtual provider
        var virtualProvider = new Provider
        {
            Id = 1,
            Name = "VanPatten Router",
            BaseUrl = "",
            ApiKey = "",
            Type = "virtual",
            IsActive = true,
            IsVirtual = true,
            Priority = 200
        };

        var freeProvider = new Provider
        {
            Id = 2,
            Name = "Free Provider",
            BaseUrl = "https://api.free.com/v1",
            ApiKey = "free-key",
            Type = "openai",
            IsActive = true,
            IsVirtual = false,
            Priority = 100,
            RateLimitJson = "{\"RequestsPerDay\": 100, \"TokensPerDay\": 10000}",
            RequestsToday = 0,
            TokensToday = 0,
            LastResetDate = DateTime.UtcNow.Date
        };

        _mockProviderService
           .Setup(s => s.ResolveAutoProviderAsync("VanPatten/auto-free", It.IsAny<int>()))
           .ReturnsAsync((virtualProvider, "auto", (string?)null));

        _mockProviderService
           .Setup(s => s.GetAllAvailableProvidersForModelAsync("VanPatten/auto-free", It.IsAny<int>()))
           .ReturnsAsync(new List<Provider> { freeProvider });

        // Setup models for auto mode (ResolveAutoProviderAsync returns "auto")
        var freeModels = new List<ProviderModel>
        {
            new()
            {
                Id = 1,
                ProviderId = freeProvider.Id,
                ModelName = "auto-free",
                IsEnabled = true,
                IsFree = true,
                Mode = "auto",
                Provider = freeProvider
            }
        };
        _mockProviderService
           .Setup(s => s.GetAllModelsForAutoModeAsync("VanPatten Router"))
           .ReturnsAsync(freeModels);

        _mockProviderService
           .Setup(s => s.GetFreeModelsForAutoModeAsync("VanPatten Router"))
           .ReturnsAsync(freeModels);

        var successResponse = new ChatCompletionResponse
        {
            Id = "chatcmpl-123",
            Object = "chat.completion",
            Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            Model = "VanPatten/auto-free",
            Choices = new List<ChatChoice>
            {
                new()
                {
                    Index = 0,
                    Message = new ChatMessage { Role = "assistant", Content = "From virtual auto-free" },
                    FinishReason = "stop"
                }
            },
            Usage = new Usage { PromptTokens = 10, CompletionTokens = 5, TotalTokens = 15 }
        };

        _mockHttp.When(HttpMethod.Post, "https://api.free.com/v1/chat/completions")
                 .Respond(HttpStatusCode.OK, "application/json", JsonSerializer.Serialize(successResponse));

        var request = new ChatCompletionRequest
        {
            Model = "VanPatten/auto-free",
            Messages = new List<ChatMessage> { new() { Role = "user", Content = "Hello" } }
        };

        // Act - Call public method
        var result = await _service.RouteChatCompletionAsync(request, "test-key");

        // Assert
        result.Should().NotBeNull();
        result.Choices[0].Message.GetTextContent().Should().Be("From virtual auto-free");
    }
}