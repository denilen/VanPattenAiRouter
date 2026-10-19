using System.Net;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
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
///     Tests for ModelRefreshService soft delete and ModelRefresh scenarios.
/// </summary>
public class ModelRefreshSoftDeleteTests
{
    private readonly string _databaseName;
    private readonly DbContextOptions<AiRouterDbContext> _options;

    public ModelRefreshSoftDeleteTests()
    {
        _databaseName = $"ModelRefreshTest_{Guid.NewGuid()}";
        _options = new DbContextOptionsBuilder<AiRouterDbContext>()
                  .UseInMemoryDatabase(_databaseName)
                  .Options;
    }

    private AiRouterDbContext CreateContext()
    {
        return new AiRouterDbContext(_options);
    }

    private ProviderService CreateProviderService(AiRouterDbContext context)
    {
        var providerLogger = new Mock<ILogger<ProviderService>>().Object;
        return new ProviderService(context, providerLogger);
    }

    [Fact]
    public async Task ModelRefresh_SoftDeletesObsoleteModels_InsteadOfHardDelete()
    {
        // Arrange
        using var context = CreateContext();
        var provider = new Provider
        {
            Name = "Test Provider",
            BaseUrl = "https://api.test.com/v1",
            ApiKey = "test-key",
            Type = "openai",
            IsActive = true,
            IsVirtual = false,
            Priority = 100,
            Models = new List<ProviderModel>
            {
                new() { ModelName = "old-model", ProviderModelName = "old-model", IsEnabled = true, IsFree = false },
                new() { ModelName = "keep-model", ProviderModelName = "keep-model", IsEnabled = true, IsFree = true }
            }
        };

        context.Providers.Add(provider);
        await context.SaveChangesAsync();

        var providerId = provider.Id;

        // Mock HTTP response with new models (old-model removed, new-model added)
        var modelsResponse = new
        {
            data = new[]
            {
                new { id = "keep-model", @object = "model", created = 1234567890, owned_by = "Test Provider" },
                new { id = "new-model", @object = "model", created = 1234567891, owned_by = "Test Provider" }
            },
            @object = "list"
        };

        var httpClientFactoryMock = new Mock<IHttpClientFactory>();
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
                   .Setup<Task<HttpResponseMessage>>("SendAsync",
                                                     ItExpr.Is<HttpRequestMessage>(req =>
                                                         req.Method == HttpMethod.Get &&
                                                         req.RequestUri!.PathAndQuery.Contains("models")),
                                                     ItExpr.IsAny<CancellationToken>())
                   .ReturnsAsync(new HttpResponseMessage
                    {
                        StatusCode = HttpStatusCode.OK,
                        Content = new StringContent(JsonSerializer.Serialize(modelsResponse), Encoding.UTF8,
                                                    "application/json")
                    });

        var client = new HttpClient(handlerMock.Object);
        httpClientFactoryMock.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(client);

        // Use the SAME database for ModelRefreshService
        var serviceProvider = new ServiceCollection()
                             .AddSingleton(httpClientFactoryMock.Object)
                             .AddDbContext<AiRouterDbContext>(o => o.UseInMemoryDatabase(_databaseName))
                             .BuildServiceProvider();

        var refreshService = new ModelRefreshService(serviceProvider,
                                                     httpClientFactoryMock.Object,
                                                     new Mock<ILogger<ModelRefreshService>>().Object,
                                                     Options.Create(new RouterOptions
                                                     {
                                                         ModelRefreshIntervalMinutes = 10,
                                                         FreeModels = new Dictionary<string, List<string>>()
                                                     }));

        // Act - Use reflection to call private method for testing
        var refreshMethod = typeof(ModelRefreshService).GetMethod("RefreshAllProvidersAsync",
                                                                  BindingFlags.NonPublic | BindingFlags.Instance);
        await (Task)refreshMethod!.Invoke(refreshService, new object[] { CancellationToken.None })!;

        // Assert - old-model should be DISABLED (IsEnabled=false), not removed
        using var assertContext = CreateContext();
        var providerService = CreateProviderService(assertContext);
        var models = await assertContext.ProviderModels.Where(m => m.ProviderId == providerId).ToListAsync();

        Assert.Equal(3, models.Count); // keep-model, new-model, old-model (disabled)
        Assert.Contains(models, m => m.ModelName == "keep-model" && m.IsEnabled);
        Assert.Contains(models, m => m.ModelName == "new-model" && m.IsEnabled);
        Assert.Contains(models, m => m.ModelName == "old-model" && !m.IsEnabled); // Soft deleted
    }

    [Fact]
    public async Task ModelRefresh_DoesNotInterruptActiveRequests_SoftDelete()
    {
        // Arrange - Simulate a request that was using a model before refresh
        using var context = CreateContext();

        // Add virtual provider (required by GetProvidersForSpecificModelAsync)
        var virtualProvider = new Provider
        {
            Name = "VanPatten",
            BaseUrl = "",
            ApiKey = "",
            Priority = 200,
            IsActive = true,
            IsVirtual = true,
            RateLimitJson = "{\"RequestsPerDay\": 10000, \"TokensPerDay\": 1000000}",
            RequestsToday = 0,
            TokensToday = 0,
            LastResetDate = DateTime.UtcNow.Date
        };
        context.Providers.Add(virtualProvider);

        var provider = new Provider
        {
            Name = "Test Provider",
            BaseUrl = "https://api.test.com/v1",
            ApiKey = "test-key",
            Type = "openai",
            IsActive = true,
            IsVirtual = false,
            Priority = 100,
            Models = new List<ProviderModel>
            {
                new()
                {
                    ModelName = "active-model", ProviderModelName = "active-model", IsEnabled = true, IsFree = true
                }
            }
        };

        context.Providers.Add(provider);
        await context.SaveChangesAsync();

        // Simulate an active request by finding the model
        var providerService = CreateProviderService(context);
        var activeModel = await providerService.GetProvidersForSpecificModelAsync("active-model");
        Assert.Single(activeModel);
        Assert.True(activeModel[0].IsEnabled);

        // Mock HTTP response - same models (no changes)
        var modelsResponse = new
        {
            data = new[]
            {
                new { id = "active-model", @object = "model", created = 1234567890, owned_by = "Test Provider" }
            },
            @object = "list"
        };

        var httpClientFactoryMock = new Mock<IHttpClientFactory>();
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
                   .Setup<Task<HttpResponseMessage>>("SendAsync",
                                                     ItExpr.Is<HttpRequestMessage>(req =>
                                                         req.Method == HttpMethod.Get &&
                                                         req.RequestUri!.PathAndQuery.Contains("models")),
                                                     ItExpr.IsAny<CancellationToken>())
                   .ReturnsAsync(new HttpResponseMessage
                    {
                        StatusCode = HttpStatusCode.OK,
                        Content = new StringContent(JsonSerializer.Serialize(new
                        {
                            data = new[]
                            {
                                new
                                {
                                    id = "active-model", @object = "model", created = 1234567890,
                                    owned_by = "Test Provider"
                                }
                            },
                            @object = "list"
                        }), Encoding.UTF8, "application/json")
                    });

        var client = new HttpClient(handlerMock.Object);
        var httpClientFactoryMock2 = new Mock<IHttpClientFactory>();
        httpClientFactoryMock2.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(client);

        // Use the SAME database for ModelRefreshService
        var serviceProvider = new ServiceCollection()
                             .AddSingleton(httpClientFactoryMock2.Object)
                             .AddDbContext<AiRouterDbContext>(o => o.UseInMemoryDatabase(_databaseName))
                             .BuildServiceProvider();

        var refreshService = new ModelRefreshService(serviceProvider,
                                                     httpClientFactoryMock2.Object,
                                                     new Mock<ILogger<ModelRefreshService>>().Object,
                                                     Options.Create(new RouterOptions
                                                     {
                                                         ModelRefreshIntervalMinutes = 10,
                                                         FreeModels = new Dictionary<string, List<string>>()
                                                     }));

        // Act - Refresh while "active request" is ongoing
        var refreshMethod = typeof(ModelRefreshService).GetMethod("RefreshAllProvidersAsync",
                                                                  BindingFlags.NonPublic | BindingFlags.Instance);
        await (Task)refreshMethod!.Invoke(refreshService, new object[] { CancellationToken.None })!;

        // Assert - Model should still be enabled and available
        using var assertContext = CreateContext();
        var assertProviderService = CreateProviderService(assertContext);
        var activeModelAfter = await assertProviderService.GetProvidersForSpecificModelAsync("active-model");
        Assert.Single(activeModelAfter);
        Assert.True(activeModelAfter[0].IsEnabled);
        Assert.Equal("active-model", activeModelAfter[0].ModelName);
    }
}