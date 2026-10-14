using System.Reflection;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VanPattenAIRouter.Data;
using VanPattenAIRouter.Models;
using VanPattenAIRouter.Services;
using Xunit;

namespace VanPattenAIRouter.Tests.Services;

public class ProviderServiceTests : IDisposable
{
    private readonly AiRouterDbContext _context;
    private readonly ILogger<ProviderService> _logger;
    private readonly ProviderService _service;

    public ProviderServiceTests()
    {
        var options = new DbContextOptionsBuilder<AiRouterDbContext>()
                     .UseInMemoryDatabase(Guid.NewGuid().ToString())
                     .Options;

        _context = new AiRouterDbContext(options);
        _logger = new LoggerFactory().CreateLogger<ProviderService>();

        _service = new ProviderService(_context, _logger);
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    [Fact]
    public async Task GetAllProvidersAsync_ReturnsEmptyList_WhenNoProviders()
    {
        // Act
        var result = await _service.GetAllProvidersAsync();

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetAllProvidersAsync_ReturnsProviders_OrderedByPriorityDesc()
    {
        // Arrange
        var provider1 = new Provider
        {
            Name = "Low Priority",
            BaseUrl = "https://api.example.com/v1",
            ApiKey = "key1",
            Priority = 10,
            IsActive = true,
            Models = new List<ProviderModel>
            {
                new() { ModelName = "model1", IsEnabled = true }
            }
        };

        var provider2 = new Provider
        {
            Name = "High Priority",
            BaseUrl = "https://api.example.com/v1",
            ApiKey = "key2",
            Priority = 100,
            IsActive = true,
            Models = new List<ProviderModel>
            {
                new() { ModelName = "model2", IsEnabled = true }
            }
        };

        _context.Providers.AddRange(provider1, provider2);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetAllProvidersAsync();

        // Assert
        result.Should().HaveCount(2);
        result[0].Name.Should().Be("High Priority");
        result[1].Name.Should().Be("Low Priority");
    }

    [Fact]
    public async Task GetAllProvidersAsync_IncludesModels()
    {
        // Arrange
        var provider = new Provider
        {
            Name = "Test Provider",
            BaseUrl = "https://api.example.com/v1",
            ApiKey = "key",
            Priority = 50,
            IsActive = true,
            Models = new List<ProviderModel>
            {
                new() { ModelName = "gpt-4", ProviderModelName = "gpt-4-turbo", IsEnabled = true },
                new() { ModelName = "gpt-3.5", ProviderModelName = "gpt-3.5-turbo", IsEnabled = false }
            }
        };

        _context.Providers.Add(provider);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetAllProvidersAsync();

        // Assert
        result.Should().HaveCount(1);
        result[0].Models.Should().HaveCount(2);
        result[0].Models[0].ModelName.Should().Be("gpt-4");
        result[0].Models[0].ProviderModelName.Should().Be("gpt-4-turbo");
        result[0].Models[0].IsEnabled.Should().BeTrue();
        result[0].Models[1].IsEnabled.Should().BeFalse();
    }

    [Fact]
    public async Task GetProviderByIdAsync_ReturnsProvider_WhenExists()
    {
        // Arrange
        var provider = new Provider
        {
            Name = "Test Provider",
            BaseUrl = "https://api.example.com/v1",
            ApiKey = "key",
            Priority = 50,
            IsActive = true,
            Models = new List<ProviderModel>
            {
                new() { ModelName = "model1", IsEnabled = true }
            }
        };

        _context.Providers.Add(provider);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetProviderByIdAsync(provider.Id);

        // Assert
        result.Should().NotBeNull();
        result!.Name.Should().Be("Test Provider");
        result.Models.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetProviderByIdAsync_ReturnsNull_WhenNotExists()
    {
        // Act
        var result = await _service.GetProviderByIdAsync(999);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task CreateProviderAsync_CreatesProviderWithModels()
    {
        // Arrange
        var providerDto = new ProviderDto
        {
            Name = "New Provider",
            BaseUrl = "https://api.new.com/v1",
            ApiKey = "new-key",
            Type = "openai",
            Headers = new Dictionary<string, string> { { "Custom-Header", "value" } },
            IsActive = true,
            Priority = 75,
            Models = new List<ProviderModelDto>
            {
                new() { ModelName = "model-a", ProviderModelName = "model-a-v2", IsEnabled = true },
                new() { ModelName = "model-b", IsEnabled = true }
            }
        };

        // Act
        var result = await _service.CreateProviderAsync(providerDto);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().BeGreaterThan(0);
        result.Name.Should().Be("New Provider");
        result.BaseUrl.Should().Be("https://api.new.com/v1");
        result.ApiKey.Should().Be("new-key");
        result.Type.Should().Be("openai");
        result.Headers.Should().ContainKey("Custom-Header");
        result.Priority.Should().Be(75);
        result.Models.Should().HaveCount(2);
        result.Models[0].ProviderModelName.Should().Be("model-a-v2");
        result.Models[1].ProviderModelName.Should().BeNull();
    }

    [Fact]
    public async Task UpdateProviderAsync_UpdatesExistingProvider()
    {
        // Arrange
        var provider = new Provider
        {
            Name = "Original",
            BaseUrl = "https://api.original.com/v1",
            ApiKey = "original-key",
            Priority = 10,
            IsActive = true,
            Models = new List<ProviderModel>
            {
                new() { ModelName = "old-model", IsEnabled = true }
            }
        };

        _context.Providers.Add(provider);
        await _context.SaveChangesAsync();

        var updateDto = new ProviderDto
        {
            Id = provider.Id,
            Name = "Updated",
            BaseUrl = "https://api.updated.com/v1",
            ApiKey = "updated-key",
            Type = "anthropic",
            Headers = new Dictionary<string, string> { { "anthropic-version", "2023-06-01" } },
            IsActive = false,
            Priority = 200,
            Models = new List<ProviderModelDto>
            {
                new() { ModelName = "new-model", ProviderModelName = "claude-3", IsEnabled = true }
            }
        };

        // Act
        var result = await _service.UpdateProviderAsync(provider.Id, updateDto);

        // Assert
        result.Should().NotBeNull();
        result!.Name.Should().Be("Updated");
        result.BaseUrl.Should().Be("https://api.updated.com/v1");
        result.ApiKey.Should().Be("updated-key");
        result.Type.Should().Be("anthropic");
        result.IsActive.Should().BeFalse();
        result.Priority.Should().Be(200);
        result.Models.Should().HaveCount(1);
        result.Models[0].ModelName.Should().Be("new-model");
        result.Models[0].ProviderModelName.Should().Be("claude-3");
    }

    [Fact]
    public async Task UpdateProviderAsync_ReturnsNull_WhenNotExists()
    {
        // Arrange
        var updateDto = new ProviderDto { Name = "Test" };

        // Act
        var result = await _service.UpdateProviderAsync(999, updateDto);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task DeleteProviderAsync_RemovesProvider()
    {
        // Arrange
        var provider = new Provider
        {
            Name = "To Delete",
            BaseUrl = "https://api.delete.com/v1",
            ApiKey = "key",
            IsActive = true,
            Models = new List<ProviderModel>
            {
                new() { ModelName = "model1", IsEnabled = true }
            }
        };

        _context.Providers.Add(provider);
        await _context.SaveChangesAsync();
        var id = provider.Id;

        // Act
        await _service.DeleteProviderAsync(id);

        // Assert
        var deleted = await _context.Providers.FindAsync(id);
        deleted.Should().BeNull();

        // Models should be cascade deleted
        var models = await _context.ProviderModels.Where(m => m.ProviderId == id).ToListAsync();
        models.Should().BeEmpty();
    }

    [Fact]
    public async Task GetAvailableModelsAsync_ReturnsOnlyEnabledModelsFromActiveProviders()
    {
        // Arrange
        var activeProvider = new Provider
        {
            Name = "Active Provider",
            BaseUrl = "https://api.active.com/v1",
            ApiKey = "key1",
            Priority = 100,
            IsActive = true,
            Models = new List<ProviderModel>
            {
                new() { ModelName = "enabled-model", IsEnabled = true },
                new() { ModelName = "disabled-model", IsEnabled = false }
            }
        };

        var inactiveProvider = new Provider
        {
            Name = "Inactive Provider",
            BaseUrl = "https://api.inactive.com/v1",
            ApiKey = "key2",
            Priority = 50,
            IsActive = false,
            Models = new List<ProviderModel>
            {
                new() { ModelName = "inactive-model", IsEnabled = true }
            }
        };

        _context.Providers.AddRange(activeProvider, inactiveProvider);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetAvailableModelsAsync();

        // Assert
        result.Should().HaveCount(1);
        result[0].Id.Should().Be("enabled-model");
        result[0].OwnedBy.Should().Be("Active Provider");
    }

    [Fact]
    public async Task GetAvailableModelsAsync_ReturnsDistinctModels()
    {
        // Arrange
        var provider1 = new Provider
        {
            Name = "Provider 1",
            BaseUrl = "https://api.p1.com/v1",
            ApiKey = "key1",
            Priority = 100,
            IsActive = true,
            Models = new List<ProviderModel>
            {
                new() { ModelName = "shared-model", IsEnabled = true }
            }
        };

        var provider2 = new Provider
        {
            Name = "Provider 2",
            BaseUrl = "https://api.p2.com/v1",
            ApiKey = "key2",
            Priority = 50,
            IsActive = true,
            Models = new List<ProviderModel>
            {
                new() { ModelName = "shared-model", IsEnabled = true },
                new() { ModelName = "unique-model", IsEnabled = true }
            }
        };

        _context.Providers.AddRange(provider1, provider2);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetAvailableModelsAsync();

        // Assert
        result.Should().HaveCount(2);
        result.Select(m => m.Id).Should().Contain(new[] { "shared-model", "unique-model" });
    }

    [Fact]
    public async Task FindProviderForModelAsync_ReturnsHighestPriorityProvider()
    {
        // Arrange
        var lowPriority = new Provider
        {
            Name = "Low Priority",
            BaseUrl = "https://api.low.com/v1",
            ApiKey = "key1",
            Priority = 10,
            IsActive = true,
            Models = new List<ProviderModel>
            {
                new() { ModelName = "gpt-4", IsEnabled = true }
            }
        };

        var highPriority = new Provider
        {
            Name = "High Priority",
            BaseUrl = "https://api.high.com/v1",
            ApiKey = "key2",
            Priority = 100,
            IsActive = true,
            Models = new List<ProviderModel>
            {
                new() { ModelName = "gpt-4", IsEnabled = true }
            }
        };

        _context.Providers.AddRange(lowPriority, highPriority);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.FindProviderForModelAsync("gpt-4");

        // Assert
        result.Should().NotBeNull();
        result!.Name.Should().Be("High Priority");
    }

    [Fact]
    public async Task FindProviderForModelAsync_ReturnsNull_WhenModelNotFound()
    {
        // Arrange
        var provider = new Provider
        {
            Name = "Test",
            BaseUrl = "https://api.test.com/v1",
            ApiKey = "key",
            IsActive = true,
            Models = new List<ProviderModel>
            {
                new() { ModelName = "existing-model", IsEnabled = true }
            }
        };

        _context.Providers.Add(provider);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.FindProviderForModelAsync("non-existent");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task FindProviderForModelAsync_ReturnsNull_WhenProviderInactive()
    {
        // Arrange
        var provider = new Provider
        {
            Name = "Inactive",
            BaseUrl = "https://api.test.com/v1",
            ApiKey = "key",
            IsActive = false,
            Models = new List<ProviderModel>
            {
                new() { ModelName = "model1", IsEnabled = true }
            }
        };

        _context.Providers.Add(provider);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.FindProviderForModelAsync("model1");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task FindProviderForModelAsync_ReturnsNull_WhenModelDisabled()
    {
        // Arrange
        var provider = new Provider
        {
            Name = "Active",
            BaseUrl = "https://api.test.com/v1",
            ApiKey = "key",
            IsActive = true,
            Models = new List<ProviderModel>
            {
                new() { ModelName = "model1", IsEnabled = false }
            }
        };

        _context.Providers.Add(provider);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.FindProviderForModelAsync("model1");

        // Assert
        result.Should().BeNull();
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData("{}", "")] // Empty JSON object parses as empty dict, not null
    [InlineData("{\"Authorization\": \"Bearer token\"}", "Authorization")]
    [InlineData("key1: value1, key2: value2", "key1")]
    public void ParseHeaders_HandlesVariousFormats(string? input, string? expectedKey)
    {
        // Arrange
        var parseMethod = typeof(ProviderService).GetMethod("ParseHeaders",
                                                            BindingFlags.NonPublic | BindingFlags.Static);

        // Act
        var result = parseMethod!.Invoke(null, new object[] { input! }) as Dictionary<string, string>;

        // Assert
        if (expectedKey == null)
        {
            result.Should().BeNull();
        }
        else if (expectedKey == "")
        {
            result.Should().NotBeNull();
            result!.Should().BeEmpty();
        }
        else
        {
            result.Should().NotBeNull();
            result!.Should().ContainKey(expectedKey);
        }
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData("{}", null)] // Empty dict returns null per implementation
    public void SerializeHeaders_HandlesNullAndEmpty(string? input, string? expected)
    {
        // Arrange
        var serializeMethod = typeof(ProviderService).GetMethod("SerializeHeaders",
                                                                BindingFlags.NonPublic | BindingFlags.Static);

        Dictionary<string, string>? dict = null;
        if (input == "{}") dict = new Dictionary<string, string>();

        // Act
        var result = serializeMethod!.Invoke(null, new object[] { dict! }) as string;

        // Assert
        if (expected == null)
            result.Should().BeNull();
        else
            result.Should().Be(expected);
    }
}