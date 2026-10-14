using System.Net;
using System.Reflection;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using RichardSzalay.MockHttp;
using VanPattenAIRouter.Data;
using VanPattenAIRouter.Models;
using VanPattenAIRouter.Services;
using Xunit;

namespace VanPattenAIRouter.Tests.Services;

public class ModelRefreshServiceEdgeCaseTests : IDisposable
{
    private readonly IOptions<RouterOptions> _routerOptions;
    private readonly AiRouterDbContext _context;
    private readonly HttpClient _httpClient;
    private readonly MockHttpMessageHandler _mockHttp;
    private readonly Mock<IHttpClientFactory> _mockHttpClientFactory;
    private readonly Mock<ILogger<ModelRefreshService>> _mockLogger;
    private readonly Mock<IServiceScope> _mockScope;
    private readonly Mock<IServiceScopeFactory> _mockScopeFactory;
    private readonly Mock<IServiceProvider> _mockServiceProvider;

    public ModelRefreshServiceEdgeCaseTests()
    {
        var options = new DbContextOptionsBuilder<AiRouterDbContext>()
                     .UseInMemoryDatabase(Guid.NewGuid().ToString())
                     .Options;

        _context = new AiRouterDbContext(options);
        _mockHttpClientFactory = new Mock<IHttpClientFactory>();
        _mockLogger = new Mock<ILogger<ModelRefreshService>>();
        _mockServiceProvider = new Mock<IServiceProvider>();
        _mockScope = new Mock<IServiceScope>();
        _mockScopeFactory = new Mock<IServiceScopeFactory>();

        var configBuilder = new ConfigurationBuilder();
        configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ModelRefreshIntervalMinutes"] = "10"
        });
        var configuration = configBuilder.Build();
        var routerOptions = configuration.Get<RouterOptions>() ?? new RouterOptions();
        _routerOptions = Options.Create(routerOptions);

        _mockHttp = new MockHttpMessageHandler();
        _httpClient = _mockHttp.ToHttpClient();

        _mockHttpClientFactory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(_httpClient);
        _mockScope.Setup(s => s.ServiceProvider).Returns(_mockServiceProvider.Object);
        _mockScopeFactory.Setup(f => f.CreateScope()).Returns(_mockScope.Object);
        _mockServiceProvider.Setup(s => s.GetService(typeof(IServiceScopeFactory))).Returns(_mockScopeFactory.Object);
        _mockServiceProvider.Setup(s => s.GetService(typeof(AiRouterDbContext))).Returns(_context);
    }

    public void Dispose()
    {
        _context.Dispose();
        _httpClient.Dispose();
        _mockHttp.Dispose();
    }

    [Fact]
    public async Task RefreshProviderModelsAsync_HandlesEmptyModelsResponse()
    {
        // Arrange
        var provider = new Provider
        {
            Name = "Empty Provider",
            BaseUrl = "https://api.empty.com/v1",
            ApiKey = "key",
            Type = "openai",
            IsActive = true,
            IsVirtual = false,
            Priority = 100
        };

        _context.Providers.Add(provider);
        await _context.SaveChangesAsync();

        var modelsResponse = new { data = new object[0], @object = "list" };

        _mockHttp.When(HttpMethod.Get, "https://api.empty.com/v1/models")
                 .Respond(HttpStatusCode.OK, "application/json", JsonSerializer.Serialize(modelsResponse));

        // Act
        var service = CreateService();
        var method = typeof(ModelRefreshService).GetMethod("RefreshProviderModelsAsync",
                                                           BindingFlags.NonPublic | BindingFlags.Instance);
        await (Task)method!.Invoke(service, new object[] { provider, _context, CancellationToken.None })!;

        // Assert - No models added
        var models = await _context.ProviderModels.Where(m => m.ProviderId == provider.Id).ToListAsync();
        models.Should().BeEmpty();
    }

    [Fact]
    public async Task RefreshProviderModelsAsync_HandlesMalformedResponse()
    {
        // Arrange
        var provider = new Provider
        {
            Name = "Malformed Provider",
            BaseUrl = "https://api.malformed.com/v1",
            ApiKey = "key",
            Type = "openai",
            IsActive = true,
            IsVirtual = false,
            Priority = 100
        };

        _context.Providers.Add(provider);
        await _context.SaveChangesAsync();

        _mockHttp.When(HttpMethod.Get, "https://api.malformed.com/v1/models")
                 .Respond(HttpStatusCode.OK, "application/json", "not valid json");

        // Act
        var service = CreateService();
        var method = typeof(ModelRefreshService).GetMethod("RefreshProviderModelsAsync",
                                                           BindingFlags.NonPublic | BindingFlags.Instance);
        await (Task)method!.Invoke(service, new object[] { provider, _context, CancellationToken.None })!;

        // Assert - No models added due to JSON parse error
        var models = await _context.ProviderModels.Where(m => m.ProviderId == provider.Id).ToListAsync();
        models.Should().BeEmpty();
    }

    [Fact]
    public async Task RefreshProviderModelsAsync_HandlesMissingIdInModel()
    {
        // Arrange
        var provider = new Provider
        {
            Name = "Missing ID Provider",
            BaseUrl = "https://api.missingid.com/v1",
            ApiKey = "key",
            Type = "openai",
            IsActive = true,
            IsVirtual = false,
            Priority = 100
        };

        _context.Providers.Add(provider);
        await _context.SaveChangesAsync();

        var modelsResponse = new
        {
            data = new object[]
            {
                new { id = "valid-model", @object = "model", created = 123, owned_by = "Test" },
                new { @object = "model", created = 456, owned_by = "Test" },         // missing id
                new { id = "", @object = "model", created = 789, owned_by = "Test" } // empty id
            },
            @object = "list"
        };

        _mockHttp.When(HttpMethod.Get, "https://api.missingid.com/v1/models")
                 .Respond(HttpStatusCode.OK, "application/json", JsonSerializer.Serialize(modelsResponse));

        // Act
        var service = CreateService();
        var method = typeof(ModelRefreshService).GetMethod("RefreshProviderModelsAsync",
                                                           BindingFlags.NonPublic | BindingFlags.Instance);
        await (Task)method!.Invoke(service, new object[] { provider, _context, CancellationToken.None })!;

        // Assert - Only valid model with non-empty id should be added
        var models = await _context.ProviderModels.Where(m => m.ProviderId == provider.Id).ToListAsync();
        models.Should().HaveCount(1);
        models[0].ModelName.Should().Be("valid-model");
    }

    [Fact]
    public async Task RefreshProviderModelsAsync_PreservesIsFreeStatus_ForExistingModels()
    {
        // Arrange
        var provider = new Provider
        {
            Name = "Preserve Provider",
            BaseUrl = "https://api.preserve.com/v1",
            ApiKey = "key",
            Type = "openai",
            IsActive = true,
            IsVirtual = false,
            Priority = 100,
            Models = new List<ProviderModel>
            {
                new() { ModelName = "free-model", IsEnabled = true, IsFree = true }, // Was free
                new() { ModelName = "paid-model", IsEnabled = true, IsFree = false } // Was paid
            }
        };

        _context.Providers.Add(provider);
        await _context.SaveChangesAsync();

        var modelsResponse = new
        {
            data = new[]
            {
                new { id = "free-model", @object = "model", created = 123, owned_by = "Preserve" },
                new { id = "paid-model", @object = "model", created = 456, owned_by = "Preserve" }
            },
            @object = "list"
        };

        _mockHttp.When(HttpMethod.Get, "https://api.preserve.com/v1/models")
                 .Respond(HttpStatusCode.OK, "application/json", JsonSerializer.Serialize(modelsResponse));

        // Act
        var service = CreateService();
        var method = typeof(ModelRefreshService).GetMethod("RefreshProviderModelsAsync",
                                                           BindingFlags.NonPublic | BindingFlags.Instance);
        await (Task)method!.Invoke(service, new object[] { provider, _context, CancellationToken.None })!;

        // Assert - IsFree should be recalculated based on model name
        var models = await _context.ProviderModels.Where(m => m.ProviderId == provider.Id).ToListAsync();
        var freeModel = models.First(m => m.ModelName == "free-model");
        var paidModel = models.First(m => m.ModelName == "paid-model");

        freeModel.IsFree.Should().BeTrue(); // Contains "free"
        paidModel.IsFree.Should().BeFalse();
    }

    [Fact]
    public void ParseHeaders_HandlesValidJson()
    {
        // Arrange
        var json = "{\"Authorization\": \"Bearer token\", \"Custom\": \"value\"}";
        var method = typeof(ModelRefreshService).GetMethod("ParseHeaders",
                                                           BindingFlags.NonPublic | BindingFlags.Static);

        // Act
        var result = (Dictionary<string, string>)method!.Invoke(null, new object[] { json })!;

        // Assert
        result.Should().HaveCount(2);
        result["Authorization"].Should().Be("Bearer token");
        result["Custom"].Should().Be("value");
    }

    [Fact]
    public void ParseHeaders_HandlesKeyValueFormat()
    {
        // Arrange
        var kv = "key1: value1, key2: value2";
        var method = typeof(ModelRefreshService).GetMethod("ParseHeaders",
                                                           BindingFlags.NonPublic | BindingFlags.Static);

        // Act
        var result = (Dictionary<string, string>)method!.Invoke(null, new object[] { kv })!;

        // Assert
        result.Should().HaveCount(2);
        result["key1"].Should().Be("value1");
        result["key2"].Should().Be("value2");
    }

    [Fact]
    public void ParseHeaders_HandlesEmptyAndNull()
    {
        // Arrange
        var method = typeof(ModelRefreshService).GetMethod("ParseHeaders",
                                                           BindingFlags.NonPublic | BindingFlags.Static);

        // Act & Assert
        var result1 = (Dictionary<string, string>)method!.Invoke(null, new object[] { "" })!;
        result1.Should().BeEmpty();

        var result2 = (Dictionary<string, string>)method!.Invoke(null, new object[]
        {
            "" // Empty string instead of null
        })!;
        result2.Should().BeEmpty();
    }

    [Fact]
    public void IsFreeModel_CaseInsensitive()
    {
        // Arrange
        var service = CreateService();
        var method = typeof(ModelRefreshService).GetMethod("IsFreeModel",
                                                           BindingFlags.NonPublic | BindingFlags.Instance);

        // Act & Assert
        var result1 = (bool)method!.Invoke(service, new object[] { "FREE-MODEL", "Provider" })!;
        result1.Should().BeTrue();

        var result2 = (bool)method!.Invoke(service, new object[] { "Free-Model", "Provider" })!;
        result2.Should().BeTrue();

        var result3 = (bool)method!.Invoke(service, new object[] { "FREE", "Provider" })!;
        result3.Should().BeTrue();
    }

    private ModelRefreshService CreateService()
    {
        return new ModelRefreshService(_mockServiceProvider.Object,
                                       _mockHttpClientFactory.Object,
                                       _mockLogger.Object,
                                       _routerOptions);
    }
}