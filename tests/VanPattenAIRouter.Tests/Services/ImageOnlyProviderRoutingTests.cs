using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using VanPattenAIRouter.Data;
using VanPattenAIRouter.Models;
using VanPattenAIRouter.Services;
using Xunit;

namespace VanPattenAIRouter.Tests.Services;

public class ImageOnlyProviderRoutingTests : IDisposable
{
    private readonly AiRouterDbContext _context;
    private readonly ProviderService _service;

    public ImageOnlyProviderRoutingTests()
    {
        var options = new DbContextOptionsBuilder<AiRouterDbContext>()
                     .UseInMemoryDatabase(Guid.NewGuid().ToString())
                     .Options;

        _context = new AiRouterDbContext(options);
        var logger = new LoggerFactory().CreateLogger<ProviderService>();
        _service = new ProviderService(_context, logger);
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    [Fact]
    public async Task GetAllAvailableProvidersForModelAsync_ExcludesImageOnlyProviders_ForNonImageTasks()
    {
        // Arrange
        var agnes = new Provider
        {
            Name = "Agnes AI",
            BaseUrl = "https://agnes-ai.com/v1",
            ApiKey = "test-key",
            Type = "openai",
            IsActive = true,
            Priority = 95,
            ImageTasksOnly = true,
            Models = new List<ProviderModel>
            {
                new() { ModelName = "shared-model", ProviderModelName = "shared-model", IsEnabled = true }
            }
        };
        var regular = new Provider
        {
            Name = "Regular",
            BaseUrl = "https://example.com/v1",
            ApiKey = "test-key",
            Type = "openai",
            IsActive = true,
            Priority = 10,
            ImageTasksOnly = false,
            Models = new List<ProviderModel>
            {
                new() { ModelName = "shared-model", ProviderModelName = "shared-model", IsEnabled = true }
            }
        };
        _context.Providers.AddRange(agnes, regular);
        await _context.SaveChangesAsync();

        // Act
        var providers = await _service.GetAllAvailableProvidersForModelAsync("shared-model", 0, includeImageOnlyProviders: false);

        // Assert
        providers.Should().ContainSingle(p => p.Name == "Regular");
        providers.Should().NotContain(p => p.Name == "Agnes AI");
    }

    [Fact]
    public async Task GetAllAvailableProvidersForModelAsync_IncludesImageOnlyProviders_ForImageTasks()
    {
        // Arrange
        var agnes = new Provider
        {
            Name = "Agnes AI",
            BaseUrl = "https://agnes-ai.com/v1",
            ApiKey = "test-key",
            Type = "openai",
            IsActive = true,
            Priority = 95,
            ImageTasksOnly = true,
            Models = new List<ProviderModel>
            {
                new() { ModelName = "dall-e-3", ProviderModelName = "dall-e-3", IsEnabled = true }
            }
        };
        _context.Providers.Add(agnes);
        await _context.SaveChangesAsync();

        // Act
        var providers = await _service.GetAllAvailableProvidersForModelAsync("dall-e-3", 0, includeImageOnlyProviders: true);

        // Assert
        providers.Should().ContainSingle(p => p.Name == "Agnes AI");
    }

    [Fact]
    public async Task GetAvailableEmbeddingProvidersAsync_ExcludesImageOnlyProviders()
    {
        // Arrange
        var agnes = new Provider
        {
            Name = "Agnes AI",
            BaseUrl = "https://agnes-ai.com/v1",
            ApiKey = "test-key",
            Type = "openai",
            IsActive = true,
            Priority = 95,
            ImageTasksOnly = true,
            Models = new List<ProviderModel>
            {
                new() { ModelName = "text-embedding-3-small", ProviderModelName = "text-embedding-3-small", IsEnabled = true }
            }
        };
        _context.Providers.Add(agnes);
        await _context.SaveChangesAsync();

        // Act
        var providers = await _service.GetAvailableEmbeddingProvidersAsync();

        // Assert
        providers.Should().BeEmpty();
    }

    [Fact]
    public async Task ResolveAutoProviderAsync_ExcludesImageOnlyProviders_ForNonImageSpecificModel()
    {
        // Arrange
        var virtualProvider = new Provider
        {
            Name = "VanPatten",
            BaseUrl = "",
            ApiKey = "",
            Type = "virtual",
            IsVirtual = true,
            IsActive = true,
            Priority = 200,
            Models = new List<ProviderModel>()
        };
        var agnes = new Provider
        {
            Name = "Agnes AI",
            BaseUrl = "https://agnes-ai.com/v1",
            ApiKey = "test-key",
            Type = "openai",
            IsActive = true,
            Priority = 95,
            ImageTasksOnly = true,
            Models = new List<ProviderModel>
            {
                new() { ModelName = "image-model", ProviderModelName = "image-model", IsEnabled = true }
            }
        };
        _context.Providers.AddRange(virtualProvider, agnes);
        await _context.SaveChangesAsync();

        // Act
        var (provider, mode, specificModel) = await _service.ResolveAutoProviderAsync("VanPatten/image-model", 0, includeImageOnlyProviders: false);

        // Assert - no eligible non-image provider for this model
        provider.Should().BeNull();
        mode.Should().Be("specific");
        specificModel.Should().Be("image-model");
    }

    [Fact]
    public async Task ResolveAutoProviderAsync_IncludesImageOnlyProviders_ForImageSpecificModel()
    {
        // Arrange
        var virtualProvider = new Provider
        {
            Name = "VanPatten",
            BaseUrl = "",
            ApiKey = "",
            Type = "virtual",
            IsVirtual = true,
            IsActive = true,
            Priority = 200,
            Models = new List<ProviderModel>()
        };
        var agnes = new Provider
        {
            Name = "Agnes AI",
            BaseUrl = "https://agnes-ai.com/v1",
            ApiKey = "test-key",
            Type = "openai",
            IsActive = true,
            Priority = 95,
            ImageTasksOnly = true,
            Models = new List<ProviderModel>
            {
                new() { ModelName = "image-model", ProviderModelName = "image-model", IsEnabled = true }
            }
        };
        _context.Providers.AddRange(virtualProvider, agnes);
        await _context.SaveChangesAsync();

        // Act
        var (provider, mode, specificModel) = await _service.ResolveAutoProviderAsync("VanPatten/image-model", 0, includeImageOnlyProviders: true);

        // Assert
        provider.Should().NotBeNull();
        provider!.IsVirtual.Should().BeTrue();
        mode.Should().Be("specific");
        specificModel.Should().Be("image-model");
    }

    [Fact]
    public async Task GetAvailableModelsAsync_ExcludesImageOnlyProviderModels()
    {
        // Arrange
        var agnes = new Provider
        {
            Name = "Agnes AI",
            BaseUrl = "https://agnes-ai.com/v1",
            ApiKey = "test-key",
            Type = "openai",
            IsActive = true,
            Priority = 95,
            ImageTasksOnly = true,
            Models = new List<ProviderModel>
            {
                new() { ModelName = "agnes-image-model", ProviderModelName = "agnes-image-model", IsEnabled = true }
            }
        };
        var regular = new Provider
        {
            Name = "Regular",
            BaseUrl = "https://example.com/v1",
            ApiKey = "test-key",
            Type = "openai",
            IsActive = true,
            Priority = 10,
            Models = new List<ProviderModel>
            {
                new() { ModelName = "chat-model", ProviderModelName = "chat-model", IsEnabled = true }
            }
        };
        _context.Providers.AddRange(agnes, regular);
        await _context.SaveChangesAsync();

        // Act
        var models = await _service.GetAvailableModelsAsync();

        // Assert
        models.Should().Contain(m => m.Id == "chat-model");
        models.Should().NotContain(m => m.Id == "agnes-image-model");
    }

    [Fact]
    public async Task AutoModeModelLists_ExcludeImageOnlyProviders_ForNonImageTasks()
    {
        // Arrange
        var virtualProvider = new Provider
        {
            Name = "VanPatten",
            BaseUrl = "",
            ApiKey = "",
            Type = "virtual",
            IsVirtual = true,
            IsActive = true,
            Priority = 200,
            Models = new List<ProviderModel>()
        };
        var agnes = new Provider
        {
            Name = "Agnes AI",
            BaseUrl = "https://agnes-ai.com/v1",
            ApiKey = "test-key",
            Type = "openai",
            IsActive = true,
            Priority = 95,
            ImageTasksOnly = true,
            Models = new List<ProviderModel>
            {
                new() { ModelName = "agnes-image", ProviderModelName = "agnes-image", IsEnabled = true, IsFree = true }
            }
        };
        var regular = new Provider
        {
            Name = "Regular",
            BaseUrl = "https://example.com/v1",
            ApiKey = "test-key",
            Type = "openai",
            IsActive = true,
            Priority = 10,
            Models = new List<ProviderModel>
            {
                new() { ModelName = "regular-chat", ProviderModelName = "regular-chat", IsEnabled = true, IsFree = true }
            }
        };
        _context.Providers.AddRange(virtualProvider, agnes, regular);
        await _context.SaveChangesAsync();

        // Act
        var freeModels = await _service.GetFreeModelsForAutoModeAsync("VanPatten", includeImageOnlyProviders: false);
        var allModels = await _service.GetAllModelsForAutoModeAsync("VanPatten", includeImageOnlyProviders: false);
        var specific = await _service.GetProvidersForSpecificModelAsync("agnes-image", "VanPatten", includeImageOnlyProviders: false);

        // Assert
        freeModels.Should().OnlyContain(m => m.ModelName == "regular-chat");
        allModels.Should().OnlyContain(m => m.ModelName == "regular-chat");
        specific.Should().BeEmpty();
    }
}
