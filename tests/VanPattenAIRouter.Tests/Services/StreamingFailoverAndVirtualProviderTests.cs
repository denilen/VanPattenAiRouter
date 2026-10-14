using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using VanPattenAIRouter.Data;
using VanPattenAIRouter.Models;
using VanPattenAIRouter.Services;
using Xunit;

namespace VanPattenAIRouter.Tests.Services;

/// <summary>
///     Tests for streaming failover and virtual provider routing.
/// </summary>
public class StreamingFailoverAndVirtualProviderTests
{
    private readonly AiRouterDbContext _context;
    private readonly RoutingService _routingService;
    private readonly Mock<IHttpClientFactory> _httpClientFactoryMock;

    public StreamingFailoverAndVirtualProviderTests()
    {
        var options = new DbContextOptionsBuilder<AiRouterDbContext>()
            .UseInMemoryDatabase(databaseName: $"StreamingTest_{System.Guid.NewGuid()}")
            .Options;
        _context = new AiRouterDbContext(options);

        var providerLogger = new Mock<ILogger<ProviderService>>().Object;
        var routingLogger = new Mock<ILogger<RoutingService>>().Object;
        var retryLogger = new Mock<ILogger<RetryPolicyService>>().Object;
        var retryConfig = Options.Create(new RetryConfig());

        var providerService = new ProviderService(_context, providerLogger);
        var retryService = new RetryPolicyService(retryConfig, retryLogger);

        _httpClientFactoryMock = new Mock<IHttpClientFactory>();
        _routingService = new RoutingService(providerService, _httpClientFactoryMock.Object, retryService, routingLogger);
    }

    private HttpClient CreateMockHttpClient(HttpStatusCode statusCode, string responseContent, string expectedModel)
    {
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.Is<HttpRequestMessage>(req =>
                    req.Method == HttpMethod.Post &&
                    req.RequestUri!.PathAndQuery.Contains("chat/completions")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = statusCode,
                Content = new StringContent(responseContent, Encoding.UTF8, "application/json")
            });

        var client = new HttpClient(handlerMock.Object)
        {
            BaseAddress = new Uri("https://api.test.com/v1/")
        };
        return client;
    }

    private ProviderService CreateProviderService()
    {
        return new ProviderService(_context, new Mock<ILogger<ProviderService>>().Object);
    }

    private RetryPolicyService CreateRetryService()
    {
        return new RetryPolicyService(Options.Create(new RetryConfig()), new Mock<ILogger<RetryPolicyService>>().Object);
    }

    private RoutingService CreateRoutingService(ProviderService providerService, IHttpClientFactory httpClientFactory, RetryPolicyService retryService)
    {
        return new RoutingService(providerService, httpClientFactory, retryService, new Mock<ILogger<RoutingService>>().Object);
    }

    [Fact]
    public async Task SingleProviderStreaming_FailoverToNextProvider_OnFailure()
    {
        // Arrange
        var provider1 = new Provider
        {
            Name = "Provider 1",
            BaseUrl = "https://api.p1.com/v1",
            ApiKey = "key1",
            Type = "openai",
            IsActive = true,
            IsVirtual = false,
            Priority = 100,
            Models = new List<ProviderModel>
            {
                new() { ModelName = "test/model", ProviderModelName = "test/model", IsEnabled = true, IsFree = true }
            }
        };

        var provider2 = new Provider
        {
            Name = "Provider 2",
            BaseUrl = "https://api.p2.com/v1",
            ApiKey = "key2",
            Type = "openai",
            IsActive = true,
            IsVirtual = false,
            Priority = 90,
            Models = new List<ProviderModel>
            {
                new() { ModelName = "test/model", ProviderModelName = "test/model", IsEnabled = true, IsFree = true }
            }
        };

        _context.Providers.AddRange(provider1, provider2);
        await _context.SaveChangesAsync();

        // Provider 1 returns 500 (failover)
        var client1 = CreateMockHttpClient(HttpStatusCode.InternalServerError, "{\"error\":\"server error\"}", "test/model");
        _httpClientFactoryMock.Setup(x => x.CreateClient("streaming")).Returns(client1);
        _httpClientFactoryMock.Setup(x => x.CreateClient("default")).Returns(client1);

        // But provider 2 succeeds
        var successResponse = "data: {\"id\":\"test\",\"object\":\"chat.completion.chunk\",\"choices\":[{\"delta\":{\"content\":\"Hello\"}}]}\n\n";
        successResponse += "data: [DONE]\n\n";
        var client2 = CreateMockHttpClient(HttpStatusCode.OK, successResponse, "test/model");
        
        // Setup factory to return different clients for different calls
        var callCount = 0;
        _httpClientFactoryMock.Setup(x => x.CreateClient("streaming"))
            .Returns(() =>
            {
                callCount++;
                return callCount == 1 ? client1 : client2;
            });
        _httpClientFactoryMock.Setup(x => x.CreateClient("default"))
            .Returns(() =>
            {
                callCount++;
                return callCount == 1 ? client1 : client2;
            });

        var providerService = CreateProviderService();
        var retryService = CreateRetryService();
        var routingService = CreateRoutingService(providerService, _httpClientFactoryMock.Object, retryService);

        // Act
        var request = new ChatCompletionRequest
        {
            Model = "test/model",
            Messages = new List<ChatMessage> { new() { Role = "user", Content = "Hello" } },
            Stream = true
        };

        var outputStream = new MemoryStream();
        await routingService.RouteChatCompletionStreamAsync(request, "test-key", outputStream, CancellationToken.None);

        // Assert - Should have tried both providers, provider 2 succeeded
        var output = Encoding.UTF8.GetString(outputStream.ToArray());
        Assert.Contains("Hello", output);
        Assert.Contains("[DONE]", output);
    }

    [Fact]
    public async Task VirtualProviderStreaming_SpecificMode_TriesAllProvidersWithSameModel()
    {
        // Arrange
        var provider1 = new Provider
        {
            Name = "Provider 1",
            BaseUrl = "https://api.p1.com/v1",
            ApiKey = "key1",
            Type = "openai",
            IsActive = true,
            IsVirtual = false,
            Priority = 100,
            Models = new List<ProviderModel>
            {
                new() { ModelName = "test/model", ProviderModelName = "test/model", IsEnabled = true, IsFree = true }
            }
        };

        var provider2 = new Provider
        {
            Name = "Provider 2",
            BaseUrl = "https://api.p2.com/v1",
            ApiKey = "key2",
            Type = "openai",
            IsActive = true,
            IsVirtual = false,
            Priority = 90,
            Models = new List<ProviderModel>
            {
                new() { ModelName = "test/model", ProviderModelName = "test/model", IsEnabled = true, IsFree = true }
            }
        };

        var virtualProvider = new Provider
        {
            Name = "VanPatten",
            BaseUrl = "",
            ApiKey = "",
            Type = "virtual",
            IsActive = true,
            IsVirtual = true,
            Priority = 200
        };

        _context.Providers.AddRange(provider1, provider2, virtualProvider);
        await _context.SaveChangesAsync();

        // Provider 1 returns 429 (rate limited - retryable)
        var client1 = CreateMockHttpClient((HttpStatusCode)429, "{\"error\":\"rate limited\"}", "test/model");

        // Provider 2 succeeds
        var successResponse = "data: {\"id\":\"test\",\"object\":\"chat.completion.chunk\",\"choices\":[{\"delta\":{\"content\":\"Hello from Provider 2\"}}]}\n\n";
        successResponse += "data: [DONE]\n\n";
        var client2 = CreateMockHttpClient(HttpStatusCode.OK, successResponse, "test/model");

        var callCount = 0;
        var httpClientFactoryMock = new Mock<IHttpClientFactory>();
        httpClientFactoryMock.Setup(x => x.CreateClient("streaming"))
            .Returns(() =>
            {
                callCount++;
                return callCount == 1 ? client1 : client2;
            });
        httpClientFactoryMock.Setup(x => x.CreateClient("default"))
            .Returns(() =>
            {
                callCount++;
                return callCount == 1 ? client1 : client2;
            });

        var providerService = CreateProviderService();
        var retryService = CreateRetryService();
        var routingService = CreateRoutingService(providerService, httpClientFactoryMock.Object, retryService);

        // Act - Use specific model mode
        var request = new ChatCompletionRequest
        {
            Model = "VanPatten/test/model",
            Messages = new List<ChatMessage> { new() { Role = "user", Content = "Hello" } },
            Stream = true
        };

        var outputStream = new MemoryStream();
        await routingService.RouteChatCompletionStreamAsync(request, "test-key", outputStream, CancellationToken.None);

        // Assert - Should have tried both providers, provider 2 succeeded
        var output = Encoding.UTF8.GetString(outputStream.ToArray());
        Assert.Contains("Hello from Provider 2", output);
    }

    [Fact]
    public async Task VirtualProviderStreaming_AutoFreeMode_TriesFreeModelsInPriorityOrder()
    {
        // Arrange
        var freeProvider1 = new Provider
        {
            Name = "Free Provider 1",
            BaseUrl = "https://api.f1.com/v1",
            ApiKey = "key1",
            Type = "openai",
            IsActive = true,
            IsVirtual = false,
            Priority = 110,
            Models = new List<ProviderModel>
            {
                new() { ModelName = "free/model", ProviderModelName = "free/model", IsEnabled = true, IsFree = true }
            }
        };

        var freeProvider2 = new Provider
        {
            Name = "Free Provider 2",
            BaseUrl = "https://api.f2.com/v1",
            ApiKey = "key2",
            Type = "openai",
            IsActive = true,
            IsVirtual = false,
            Priority = 100,
            Models = new List<ProviderModel>
            {
                new() { ModelName = "free/model", ProviderModelName = "free/model", IsEnabled = true, IsFree = true }
            }
        };

        var virtualProvider = new Provider
        {
            Name = "VanPatten",
            BaseUrl = "",
            ApiKey = "",
            Type = "virtual",
            IsActive = true,
            IsVirtual = true,
            Priority = 200
        };

        _context.Providers.AddRange(freeProvider1, freeProvider2, virtualProvider);
        await _context.SaveChangesAsync();

        // Provider 1 returns 503 (retryable)
        var client1 = CreateMockHttpClient(HttpStatusCode.ServiceUnavailable, "{\"error\":\"service unavailable\"}", "free/model");

        // Provider 2 succeeds
        var successResponse = "data: {\"id\":\"test\",\"object\":\"chat.completion.chunk\",\"choices\":[{\"delta\":{\"content\":\"Success\"}}]}\n\n";
        successResponse += "data: [DONE]\n\n";
        var client2 = CreateMockHttpClient(HttpStatusCode.OK, successResponse, "free/model");

        var callCount = 0;
        var httpClientFactoryMock = new Mock<IHttpClientFactory>();
        httpClientFactoryMock.Setup(x => x.CreateClient("streaming"))
            .Returns(() =>
            {
                callCount++;
                return callCount == 1 ? client1 : client2;
            });
        httpClientFactoryMock.Setup(x => x.CreateClient("default"))
            .Returns(() =>
            {
                callCount++;
                return callCount == 1 ? client1 : client2;
            });

        var providerService = CreateProviderService();
        var retryService = CreateRetryService();
        var routingService = CreateRoutingService(providerService, httpClientFactoryMock.Object, retryService);

        // Act - Use auto-free mode
        var request = new ChatCompletionRequest
        {
            Model = "VanPatten/auto-free",
            Messages = new List<ChatMessage> { new() { Role = "user", Content = "Hello" } },
            Stream = true
        };

        var outputStream = new MemoryStream();
        await routingService.RouteChatCompletionStreamAsync(request, "test-key", outputStream, CancellationToken.None);

        // Assert - Should have tried both providers, provider 2 succeeded
        var output = Encoding.UTF8.GetString(outputStream.ToArray());
        Assert.Contains("Success", output);
    }
}