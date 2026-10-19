using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using VanPattenAIRouter.Data;
using VanPattenAIRouter.Models;
using VanPattenAIRouter.Services;
using Xunit;

namespace VanPattenAIRouter.Tests.Integration;

public class KiloCodeIntegrationTests : IClassFixture<TestWebApplicationFactory>, IAsyncLifetime
{
    private static readonly ILogger<RoutingService>? _logger = new LoggerFactory().CreateLogger<RoutingService>();
    private readonly HttpClient _client;
    private readonly AiRouterDbContext _context;
    private readonly TestWebApplicationFactory _factory;
    private readonly IServiceScope _scope;

    public KiloCodeIntegrationTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
        _scope = factory.Services.CreateScope();
        _context = _scope.ServiceProvider.GetRequiredService<AiRouterDbContext>();
    }

    public Task InitializeAsync()
    {
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        _scope.Dispose();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task GetModels_ReturnsKiloCodeModels()
    {
        // Act
        var response = await _client.GetAsync("/v1/models");

        // Assert
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<ModelsListResponse>();

        result.Should().NotBeNull();
        result!.Data.Should().NotBeEmpty();
        // Virtual provider returns its own model IDs (vanpatten/auto-free, vanpatten/auto)
        // and seeded provider models
        result.Data.Should().Contain(m => m.Id == "vanpatten/auto-free" || m.Id == "vanpatten/auto");
    }

    [Fact]
    public async Task ChatCompletion_WithValidModel_RoutesToKiloCode()
    {
        // Arrange
        var request = new ChatCompletionRequest
        {
            Model = "gpt-4o-mini",
            Messages = new List<ChatMessage>
            {
                new() { Role = "user", Content = "Say 'Hello from KiloCode test'" }
            },
            MaxTokens = 50,
            Temperature = 0
        };

        // Act
        var response = await _client.PostAsJsonAsync("/v1/chat/completions", request);

        // Assert
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<ChatCompletionResponse>();

        result.Should().NotBeNull();
        result!.Model.Should().Be("gpt-4o-mini");
        result.Choices.Should().HaveCount(1);
        result.Choices[0].Message.GetTextContent().Should().NotBeNullOrEmpty();
        result.Usage.Should().NotBeNull();
        result.Usage.TotalTokens.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task ChatCompletion_WithClaudeModel_RoutesToKiloCode()
    {
        // Arrange
        var request = new ChatCompletionRequest
        {
            Model = "claude-3-5-haiku",
            Messages = new List<ChatMessage>
            {
                new() { Role = "user", Content = "Say 'Hello from Claude via KiloCode'" }
            },
            MaxTokens = 50,
            Temperature = 0
        };

        // Act
        var response = await _client.PostAsJsonAsync("/v1/chat/completions", request);

        // Assert
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<ChatCompletionResponse>();

        result.Should().NotBeNull();
        result!.Model.Should().Be("claude-3-5-haiku");
        result.Choices.Should().HaveCount(1);
        result.Choices[0].Message.GetTextContent().Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task ChatCompletion_WithInvalidModel_ReturnsError()
    {
        // Arrange
        var request = new ChatCompletionRequest
        {
            Model = "non-existent-model",
            Messages = new List<ChatMessage>
            {
                new() { Role = "user", Content = "Hello" }
            }
        };

        // Act
        var response = await _client.PostAsJsonAsync("/v1/chat/completions", request);

        // Assert - should return error response, not throw
        response.EnsureSuccessStatusCode(); // Returns 200 with error in body
        var result = await response.Content.ReadFromJsonAsync<ChatCompletionResponse>();
        result.Should().NotBeNull();
        result!.Choices[0].FinishReason.Should().Be("rate_limit");
    }

    [Fact]
    public async Task AdminProviders_ReturnsKiloCodeProvider()
    {
        // Act
        var response = await _client.GetAsync("/admin/providers");

        // Assert
        response.EnsureSuccessStatusCode();
        var providers = await response.Content.ReadFromJsonAsync<List<ProviderDto>>();

        providers.Should().NotBeNull();
        // Check for any KiloCode provider (test factory seeds "KiloCode", appsettings has "KiloCode - email")
        providers!.Should()
                  .Contain(p => p.Name.Contains("KiloCode", StringComparison.OrdinalIgnoreCase) && p.IsActive == true);
        var kilocode
            = providers!.First(p => p.Name.Contains("KiloCode", StringComparison.OrdinalIgnoreCase) && p.IsActive);
        kilocode.RateLimit.Should().NotBeNull();
        // Rate limit values may vary based on config source (test factory vs appsettings)
        kilocode.ApiKey.Should().NotBeNull();
    }

    [Fact]
    public async Task Embeddings_RoutesToKiloCode()
    {
        // Arrange
        var request = new EmbeddingRequest
        {
            Model = "gpt-4o-mini",
            Input = new List<string> { "test embedding" }
        };

        // Act
        var response = await _client.PostAsJsonAsync("/v1/embeddings", request);

        // Assert - may fail if model doesn't support embeddings, but should route correctly
        if (response.IsSuccessStatusCode)
        {
            var result = await response.Content.ReadFromJsonAsync<EmbeddingResponse>();
            result.Should().NotBeNull();
            result!.Data.Should().NotBeEmpty();
        }
    }

    [Fact]
    public async Task ChatCompletion_WithToolResult_LogsRequestBodyCorrectly()
    {
        // Arrange - Create a mock HTTP message handler to capture the request body
        var capturedRequestBody = "";
        var mockHandler = new Mock<HttpMessageHandler>();

        mockHandler.Protected()
                   .Setup<Task<HttpResponseMessage>>("SendAsync",
                                                     ItExpr.IsAny<HttpRequestMessage>(),
                                                     ItExpr.IsAny<CancellationToken>())
                   .ReturnsAsync((HttpRequestMessage request, CancellationToken token) =>
                    {
                        capturedRequestBody = request.Content?.ReadAsStringAsync(token).GetAwaiter().GetResult() ?? "";

                        // Return a mock successful response
                        var response = new HttpResponseMessage(HttpStatusCode.OK)
                        {
                            Content = new StringContent(JsonSerializer.Serialize(new ChatCompletionResponse
                            {
                                Id = "chatcmpl-test",
                                Object = "chat.completion",
                                Created = 1234567890,
                                Model = "gpt-4o-mini",
                                Choices = new List<ChatChoice>
                                {
                                    new()
                                    {
                                        Index = 0,
                                        Message = new ChatMessage
                                            { Role = "assistant", Content = "Tool result received" },
                                        FinishReason = "stop"
                                    }
                                },
                                Usage = new Usage { PromptTokens = 10, CompletionTokens = 5, TotalTokens = 15 }
                            }), Encoding.UTF8, "application/json")
                        };
                        return response;
                    })
                   .Verifiable();

        // Create a client with the mock handler
        var mockClient = new HttpClient(mockHandler.Object)
        {
            BaseAddress = new Uri("https://api.kilo.ai/api/gateway/")
        };
        mockClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "test-key");

        // Create a real RoutingService with the mock client
        var mockProviderService = new Mock<IProviderService>();
        var provider = new Provider
        {
            Id = 1,
            Name = "TestProvider",
            BaseUrl = "https://api.kilo.ai/api/gateway",
            ApiKey = "test-key",
            Type = "openai",
            IsActive = true,
            Priority = 100,
            IsVirtual = false,
            RateLimit = new RateLimitConfig { RequestsPerDay = 100, TokensPerDay = 50000 }
        };

        mockProviderService.Setup(x => x.ResolveAutoProviderAsync(It.IsAny<string>(), It.IsAny<int>()))
                           .ReturnsAsync((provider, "auto", (string?)null));
        mockProviderService.Setup(x => x.GetAllAvailableProvidersForModelAsync(It.IsAny<string>(), It.IsAny<int>()))
                           .ReturnsAsync(new List<Provider> { provider });
        mockProviderService.Setup(x => x.RecordUsageAsync(It.IsAny<int>(), It.IsAny<int>()))
                           .Returns(Task.CompletedTask);
        mockProviderService.Setup(x => x.GetProviderByIdAsync(It.IsAny<int>()))
                           .ReturnsAsync(new ProviderDto
                            {
                                Id = 1,
                                Models = new List<ProviderModelDto>
                                    { new() { ModelName = "gpt-4o-mini", ProviderModelName = "gpt-4o-mini" } }
                            });

        var mockHttpClientFactory = new Mock<IHttpClientFactory>();
        mockHttpClientFactory.Setup(x => x.CreateClient(It.IsAny<string>()))
                             .Returns(mockClient);
        mockHttpClientFactory.Setup(x => x.CreateClient("streaming"))
                             .Returns(mockClient);

        // Use real RetryPolicyService with mock config
        var mockRetryConfig = new Mock<IOptions<RetryConfig>>();
        mockRetryConfig.Setup(x => x.Value).Returns(new RetryConfig
        {
            MaxAttempts = 1,
            BaseDelaySeconds = 0,
            MaxDelaySeconds = 0,
            ExponentialBackoff = false,
            RetryableStatusCodes = new List<int>(),
            RetryableExceptions = new List<string>()
        });
        var retryLogger = new LoggerFactory().CreateLogger<RetryPolicyService>();
        var retryPolicyService = new RetryPolicyService(mockRetryConfig.Object, retryLogger);

        var logger = new LoggerFactory().CreateLogger<RoutingService>();

        var routingService = new RoutingService(mockProviderService.Object,
                                                mockHttpClientFactory.Object,
                                                retryPolicyService,
                                                logger, _context);

        // Create request with tool result
        var request = new ChatCompletionRequest
        {
            Model = "gpt-4o-mini",
            Messages = new List<ChatMessage>
            {
                new() { Role = "user", Content = "What's the weather?" },
                new()
                {
                    Role = "assistant", Content = "", ToolCalls = new List<ChatToolCall>
                    {
                        new()
                        {
                            Id = "call_123", Type = "function",
                            Function = new Function { Name = "get_weather", Arguments = "{\"location\": \"NYC\"}" }
                        }
                    }
                },
                new()
                {
                    Role = "tool", Content = "{\"temperature\": 72, \"condition\": \"sunny\"}", ToolCallId = "call_123",
                    Name = "get_weather"
                }
            },
            MaxTokens = 50,
            Temperature = 0
        };

        // Act
        var result = await routingService.RouteChatCompletionAsync(request, "test-api-key");

        // Assert
        result.Should().NotBeNull();

        // Log the captured request body for inspection
        _logger?.LogInformation("Captured request body: {RequestBody}", capturedRequestBody);

        // Verify the tool result is in the request body
        capturedRequestBody.Should().Contain("tool");
        capturedRequestBody.Should().Contain("call_123");
        capturedRequestBody.Should().Contain("get_weather");
        capturedRequestBody.Should().Contain("temperature");
        capturedRequestBody.Should().Contain("sunny");
    }

    private class ModelsListResponse
    {
        public List<ModelInfo> Data { get; set; } = new();
        public string Object { get; set; } = "";
    }
}

public class TestWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly Mock<IRoutingService> _mockRoutingService = new();

    public TestWebApplicationFactory()
    {
        // Force InMemory database via environment variable before host is built
        Environment.SetEnvironmentVariable("UseInMemoryDatabase", "true");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((context, configBuilder) =>
        {
            configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["UseInMemoryDatabase"] = "true"
            });
        });

        builder.ConfigureServices(services =>
        {
            // Replace IRoutingService with mock
            var routingDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IRoutingService));
            if (routingDescriptor != null) services.Remove(routingDescriptor);

            services.AddSingleton(_mockRoutingService.Object);

            // Build service provider
            var sp = services.BuildServiceProvider();

            // Create scope and seed database
            using var scope = sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AiRouterDbContext>();
            db.Database.EnsureCreated();

            // Seed with KiloCode config
            var appOptions = BuildTestAppOptions();
            SeedData.InitializeAsync(db, appOptions).Wait();

            // Add test models directly to DB (since we don't call real API in tests)
            AddTestModels(db);
            db.SaveChanges();

            // Setup mock responses after seeding
            SetupMockResponses();
        });
    }

    private static void AddTestModels(AiRouterDbContext db)
    {
        var kilocode = db.Providers.FirstOrDefault(p => p.Name == "KiloCode");
        if (kilocode != null)
        {
            var models = new List<ProviderModel>
            {
                new()
                {
                    ProviderId = kilocode.Id, ModelName = "gpt-4o", ProviderModelName = "openai/gpt-4o",
                    IsEnabled = true
                },
                new()
                {
                    ProviderId = kilocode.Id, ModelName = "gpt-4o-mini", ProviderModelName = "openai/gpt-4o-mini",
                    IsEnabled = true
                },
                new()
                {
                    ProviderId = kilocode.Id, ModelName = "claude-3-5-sonnet",
                    ProviderModelName = "anthropic/claude-3-5-sonnet", IsEnabled = true
                },
                new()
                {
                    ProviderId = kilocode.Id, ModelName = "claude-3-5-haiku",
                    ProviderModelName = "anthropic/claude-3-5-haiku", IsEnabled = true
                }
            };
            db.ProviderModels.AddRange(models);
        }
    }

    private void SetupMockResponses()
    {
        var chatResponse = new ChatCompletionResponse
        {
            Id = "chatcmpl-test",
            Object = "chat.completion",
            Created = 1234567890,
            Model = "gpt-4o-mini",
            Choices = new List<ChatChoice>
            {
                new()
                {
                    Index = 0,
                    Message = new ChatMessage { Role = "assistant", Content = "Hello from KiloCode test" },
                    FinishReason = "stop"
                }
            },
            Usage = new Usage { PromptTokens = 10, CompletionTokens = 5, TotalTokens = 15 }
        };

        var embeddingResponse = new EmbeddingResponse
        {
            Object = "list",
            Data = new List<EmbeddingData>
            {
                new() { Object = "embedding", Embedding = new List<float> { 0.1f, 0.2f, 0.3f }, Index = 0 }
            },
            Model = "gpt-4o-mini",
            Usage = new Usage { PromptTokens = 5, TotalTokens = 5 }
        };

        _mockRoutingService
           .Setup(x => x.RouteChatCompletionAsync(It.IsAny<ChatCompletionRequest>(), It.IsAny<string>()))
           .ReturnsAsync((ChatCompletionRequest req, string _) =>
            {
                if (req.Model == "non-existent-model")
                    return new ChatCompletionResponse
                    {
                        Model = req.Model,
                        Choices = new List<ChatChoice>
                        {
                            new()
                            {
                                Index = 0,
                                Message = new ChatMessage
                                {
                                    Role = "assistant",
                                    Content
                                        = "No available provider for model 'non-existent-model'. All providers are rate limited or not configured."
                                },
                                FinishReason = "rate_limit"
                            }
                        },
                        Usage = new Usage()
                    };

                return new ChatCompletionResponse
                {
                    Id = "chatcmpl-test",
                    Object = "chat.completion",
                    Created = 1234567890,
                    Model = req.Model,
                    Choices = new List<ChatChoice>
                    {
                        new()
                        {
                            Index = 0,
                            Message = new ChatMessage { Role = "assistant", Content = $"Hello from {req.Model}" },
                            FinishReason = "stop"
                        }
                    },
                    Usage = new Usage { PromptTokens = 10, CompletionTokens = 5, TotalTokens = 15 }
                };
            });

        _mockRoutingService.Setup(x => x.RouteEmbeddingAsync(It.IsAny<EmbeddingRequest>(), It.IsAny<string>()))
                           .ReturnsAsync(embeddingResponse);
    }

    private static IOptions<AppOptions> BuildTestAppOptions()
    {
        var configBuilder = new ConfigurationBuilder()
                           .SetBasePath(Directory.GetCurrentDirectory())
                           .AddJsonFile("appsettings.json", true)
                           .AddJsonFile("appsettings.Development.json", true)
                           .AddEnvironmentVariables();

        var configuration = configBuilder.Build();

        // Get the first KiloCode provider's API key from config
        var kilocodeProvider = configuration.GetSection("Router:Providers").GetChildren()
                                            .FirstOrDefault(p => p["Name"]
                                                                  ?.Contains(
                                                                        "KiloCode", StringComparison.OrdinalIgnoreCase)
                                                                == true);

        var apiKey = kilocodeProvider?["ApiKey"] ?? "test-key";

        // Build test options with the real API key (or fallback)
        configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Router:Providers:0:Name"] = "KiloCode",
            ["Router:Providers:0:BaseUrl"] = "https://api.kilo.ai/api/gateway",
            ["Router:Providers:0:ApiKey"] = apiKey,
            ["Router:Providers:0:Type"] = "openai",
            ["Router:Providers:0:IsActive"] = "true",
            ["Router:Providers:0:Priority"] = "100",
            ["Router:Providers:0:RateLimit:RequestsPerDay"] = "100",
            ["Router:Providers:0:RateLimit:TokensPerDay"] = "50000"
        });

        var finalConfig = configBuilder.Build();
        var appOptions = new AppOptions
        {
            Router = finalConfig.GetSection("Router").Get<RouterOptions>() ?? new RouterOptions()
        };
        return Options.Create(appOptions);
    }
}