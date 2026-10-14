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

public class ModelRefreshServiceTests : IDisposable
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

    public ModelRefreshServiceTests()
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
        // Use GetService instead of GetRequiredService (extension method)
        _mockServiceProvider.Setup(s => s.GetService(typeof(AiRouterDbContext))).Returns(_context);
    }

    public void Dispose()
    {
        _context.Dispose();
        _httpClient.Dispose();
        _mockHttp.Dispose();
    }

    [Fact]
    public async Task RefreshProviderModelsAsync_SkipsVirtualProvider()
    {
        // Arrange
        var virtualProvider = new Provider
        {
            Name = "Virtual Provider",
            BaseUrl = "",
            ApiKey = "",
            Type = "virtual",
            IsActive = true,
            IsVirtual = true,
            Priority = 100
        };

        _context.Providers.Add(virtualProvider);
        await _context.SaveChangesAsync();

        // Act
        var service = CreateService();
        var method = typeof(ModelRefreshService).GetMethod("RefreshProviderModelsAsync",
                                                           BindingFlags.NonPublic | BindingFlags.Instance);
        await (Task)method!.Invoke(service, new object[] { virtualProvider, _context, CancellationToken.None })!;

        // Assert - No HTTP calls should be made for virtual provider
        _mockHttp.VerifyNoOutstandingExpectation();
    }

    [Fact]
    public async Task RefreshProviderModelsAsync_FetchesModels_FromHttpEndpoint()
    {
        // Arrange
        var provider = new Provider
        {
            Name = "Test Provider",
            BaseUrl = "https://api.test.com/v1",
            ApiKey = "test-key",
            Type = "openai",
            IsActive = true,
            IsVirtual = false,
            Priority = 100
        };

        _context.Providers.Add(provider);
        await _context.SaveChangesAsync();

        var modelsResponse = new
        {
            data = new[]
            {
                new { id = "model-1", @object = "model", created = 1234567890, owned_by = "Test Provider" },
                new { id = "model-2", @object = "model", created = 1234567891, owned_by = "Test Provider" }
            },
            @object = "list"
        };

        _mockHttp.When(HttpMethod.Get, "https://api.test.com/v1/models")
                 .Respond(HttpStatusCode.OK, "application/json", JsonSerializer.Serialize(modelsResponse));

        // Act
        var service = CreateService();
        var method = typeof(ModelRefreshService).GetMethod("RefreshProviderModelsAsync",
                                                           BindingFlags.NonPublic | BindingFlags.Instance);
        await (Task)method!.Invoke(service, new object[] { provider, _context, CancellationToken.None })!;

        // Assert
        var models = await _context.ProviderModels.Where(m => m.ProviderId == provider.Id).ToListAsync();
        models.Should().HaveCount(2);
        models.Should().Contain(m => m.ModelName == "model-1");
        models.Should().Contain(m => m.ModelName == "model-2");
    }

    [Fact]
    public async Task RefreshProviderModelsAsync_HandlesHttpError()
    {
        // Arrange
        var provider = new Provider
        {
            Name = "Error Provider",
            BaseUrl = "https://api.error.com/v1",
            ApiKey = "test-key",
            Type = "openai",
            IsActive = true,
            IsVirtual = false,
            Priority = 100
        };

        _context.Providers.Add(provider);
        await _context.SaveChangesAsync();

        _mockHttp.When(HttpMethod.Get, "https://api.error.com/v1/models")
                 .Respond(HttpStatusCode.InternalServerError, "application/json", "{\"error\": \"Server error\"}");

        // Act
        var service = CreateService();
        var method = typeof(ModelRefreshService).GetMethod("RefreshProviderModelsAsync",
                                                           BindingFlags.NonPublic | BindingFlags.Instance);
        await (Task)method!.Invoke(service, new object[] { provider, _context, CancellationToken.None })!;

        // Assert - No models should be added on error
        var models = await _context.ProviderModels.Where(m => m.ProviderId == provider.Id).ToListAsync();
        models.Should().BeEmpty();
    }

    [Fact]
    public async Task RefreshProviderModelsAsync_HandlesTimeout()
    {
        // Arrange
        var provider = new Provider
        {
            Name = "Timeout Provider",
            BaseUrl = "https://api.timeout.com/v1",
            ApiKey = "test-key",
            Type = "openai",
            IsActive = true,
            IsVirtual = false,
            Priority = 100
        };

        _context.Providers.Add(provider);
        await _context.SaveChangesAsync();

        _mockHttp.When(HttpMethod.Get, "https://api.timeout.com/v1/models")
                 .Throw(new TaskCanceledException("Timeout"));

        // Act
        var service = CreateService();
        var method = typeof(ModelRefreshService).GetMethod("RefreshProviderModelsAsync",
                                                           BindingFlags.NonPublic | BindingFlags.Instance);
        await (Task)method!.Invoke(service, new object[] { provider, _context, CancellationToken.None })!;

        // Assert - No models should be added on timeout
        var models = await _context.ProviderModels.Where(m => m.ProviderId == provider.Id).ToListAsync();
        models.Should().BeEmpty();
    }

    [Fact]
    public async Task RefreshProviderModelsAsync_UpdatesExistingModels()
    {
        // Arrange
        var provider = new Provider
        {
            Name = "Update Provider",
            BaseUrl = "https://api.update.com/v1",
            ApiKey = "test-key",
            Type = "openai",
            IsActive = true,
            IsVirtual = false,
            Priority = 100,
            Models = new List<ProviderModel>
            {
                new() { ModelName = "old-model", ProviderModelName = "old-model", IsEnabled = true, IsFree = false }
            }
        };

        _context.Providers.Add(provider);
        await _context.SaveChangesAsync();

        var modelsResponse = new
        {
            data = new[]
            {
                new { id = "new-model", @object = "model", created = 1234567890, owned_by = "Update Provider" },
                new { id = "old-model", @object = "model", created = 1234567891, owned_by = "Update Provider" }
            },
            @object = "list"
        };

        _mockHttp.When(HttpMethod.Get, "https://api.update.com/v1/models")
                 .Respond(HttpStatusCode.OK, "application/json", JsonSerializer.Serialize(modelsResponse));

        // Act
        var service = CreateService();
        var method = typeof(ModelRefreshService).GetMethod("RefreshProviderModelsAsync",
                                                           BindingFlags.NonPublic | BindingFlags.Instance);
        await (Task)method!.Invoke(service, new object[] { provider, _context, CancellationToken.None })!;

        // Assert
        var models = await _context.ProviderModels.Where(m => m.ProviderId == provider.Id).ToListAsync();
        models.Should().HaveCount(2);
        models.Should().Contain(m => m.ModelName == "old-model");
        models.Should().Contain(m => m.ModelName == "new-model");
    }

    [Fact]
    public async Task RefreshProviderModelsAsync_DisablesObsoleteModels()
    {
        // Arrange
        var provider = new Provider
        {
            Name = "Remove Provider",
            BaseUrl = "https://api.remove.com/v1",
            ApiKey = "test-key",
            Type = "openai",
            IsActive = true,
            IsVirtual = false,
            Priority = 100,
            Models = new List<ProviderModel>
            {
                new() { ModelName = "to-remove", ProviderModelName = "to-remove", IsEnabled = true, IsFree = false },
                new() { ModelName = "to-keep", ProviderModelName = "to-keep", IsEnabled = true, IsFree = false }
            }
        };

        _context.Providers.Add(provider);
        await _context.SaveChangesAsync();

        var modelsResponse = new
        {
            data = new[]
            {
                new { id = "to-keep", @object = "model", created = 1234567890, owned_by = "Remove Provider" },
                new { id = "new-model", @object = "model", created = 1234567891, owned_by = "Remove Provider" }
            },
            @object = "list"
        };

        _mockHttp.When(HttpMethod.Get, "https://api.remove.com/v1/models")
                 .Respond(HttpStatusCode.OK, "application/json", JsonSerializer.Serialize(modelsResponse));

        // Act
        var service = CreateService();
        var method = typeof(ModelRefreshService).GetMethod("RefreshProviderModelsAsync",
                                                           BindingFlags.NonPublic | BindingFlags.Instance);
        await (Task)method!.Invoke(service, new object[] { provider, _context, CancellationToken.None })!;

        // Assert - Obsolete model should be DISABLED (soft delete), not removed
        var models = await _context.ProviderModels.Where(m => m.ProviderId == provider.Id).ToListAsync();
        models.Should().HaveCount(3); // to-keep, new-model, to-remove (disabled)
        models.Should().Contain(m => m.ModelName == "to-keep" && m.IsEnabled);
        models.Should().Contain(m => m.ModelName == "new-model" && m.IsEnabled);
        models.Should().Contain(m => m.ModelName == "to-remove" && !m.IsEnabled); // Soft deleted
    }

    [Theory]
    [InlineData("free-model", true)]
    [InlineData("model-free", true)]
    [InlineData("free", true)]
    [InlineData("kilo-auto/free", true)]
    [InlineData("kilo-auto/efficient", true)]
    [InlineData("google/gemini-2.0-flash-exp:free", true)]
    [InlineData("meta-llama/llama-3.1-8b-instruct:free", true)]
    [InlineData("stepfun/step-5-preview-free", true)]
    [InlineData("model:free", true)]
    [InlineData("model-free-variant", true)]
    [InlineData("paid-model", false)]
    [InlineData("gpt-4", false)]
    [InlineData("claude-3", false)]
    public void IsFreeModel_DetectsFreeModels_Correctly(string modelName, bool expected)
    {
        // Arrange
        var providerName = "KiloCode";
        var service = CreateService();
        var method = typeof(ModelRefreshService).GetMethod("IsFreeModel",
                                                           BindingFlags.NonPublic | BindingFlags.Instance);

        // Act
        var result = (bool)method!.Invoke(service, new object[] { modelName, providerName })!;

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("Local Ollama", "any-model", true)]
    [InlineData("OpenRouter", "model:free", true)]
    [InlineData("OpenRouter", "model-free", true)]
    [InlineData("OpenRouter", "paid-model", false)]
    public void IsFreeModel_ProviderSpecificRules_WorkCorrectly(string providerName, string modelName, bool expected)
    {
        // Arrange
        var service = CreateService();
        var method = typeof(ModelRefreshService).GetMethod("IsFreeModel",
                                                           BindingFlags.NonPublic | BindingFlags.Instance);

        // Act
        var result = (bool)method!.Invoke(service, new object[] { modelName, providerName })!;

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public async Task RefreshAllProvidersAsync_OnlyRefreshesActiveNonVirtualProviders()
    {
        // Arrange
        var activeRealProvider = new Provider
        {
            Name = "Active Real",
            BaseUrl = "https://api.active.com/v1",
            ApiKey = "key",
            Type = "openai",
            IsActive = true,
            IsVirtual = false,
            Priority = 100
        };

        var inactiveProvider = new Provider
        {
            Name = "Inactive",
            BaseUrl = "https://api.inactive.com/v1",
            ApiKey = "key",
            Type = "openai",
            IsActive = false,
            IsVirtual = false,
            Priority = 50
        };

        var virtualProvider = new Provider
        {
            Name = "Virtual",
            BaseUrl = "",
            ApiKey = "",
            Type = "virtual",
            IsActive = true,
            IsVirtual = true,
            Priority = 200
        };

        _context.Providers.AddRange(activeRealProvider, inactiveProvider, virtualProvider);
        await _context.SaveChangesAsync();

        var modelsResponse = new
        {
            data = new[] { new { id = "model-1", @object = "model", created = 123, owned_by = "Active Real" } },
            @object = "list"
        };

        _mockHttp.When(HttpMethod.Get, "https://api.active.com/v1/models")
                 .Respond(HttpStatusCode.OK, "application/json", JsonSerializer.Serialize(modelsResponse));

        // Act
        var service = CreateService();
        var method = typeof(ModelRefreshService).GetMethod("RefreshAllProvidersAsync",
                                                           BindingFlags.NonPublic | BindingFlags.Instance);
        await (Task)method!.Invoke(service, new object[] { CancellationToken.None })!;

        // Assert - Only active real provider should be refreshed
        var models = await _context.ProviderModels.Where(m => m.ProviderId == activeRealProvider.Id).ToListAsync();
        models.Should().HaveCount(1);

        var inactiveModels
            = await _context.ProviderModels.Where(m => m.ProviderId == inactiveProvider.Id).ToListAsync();
        inactiveModels.Should().BeEmpty();

        var virtualModels = await _context.ProviderModels.Where(m => m.ProviderId == virtualProvider.Id).ToListAsync();
        virtualModels.Should().BeEmpty();
    }

    private ModelRefreshService CreateService()
    {
        return new ModelRefreshService(_mockServiceProvider.Object,
                                       _mockHttpClientFactory.Object,
                                       _mockLogger.Object,
                                       _routerOptions);
    }
}