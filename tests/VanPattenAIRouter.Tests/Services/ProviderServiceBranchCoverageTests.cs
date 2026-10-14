using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VanPattenAIRouter.Data;
using VanPattenAIRouter.Models;
using VanPattenAIRouter.Services;
using Xunit;

namespace VanPattenAIRouter.Tests.Services;

public class ProviderServiceBranchCoverageTests : IDisposable
{
    private readonly AiRouterDbContext _context;
    private readonly ILogger<ProviderService> _logger;
    private readonly ProviderService _service;

    public ProviderServiceBranchCoverageTests()
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
    public async Task GetAllProvidersAsync_IncludesVirtualProviders()
    {
        // Arrange
        var virtualProvider = new Provider
        {
            Name = "Virtual",
            BaseUrl = "",
            ApiKey = "",
            Type = "virtual",
            IsActive = true,
            IsVirtual = true,
            Priority = 200,
            Models = new List<ProviderModel>
            {
                new() { ModelName = "virtual-model", IsEnabled = true }
            }
        };

        var realProvider = new Provider
        {
            Name = "Real",
            BaseUrl = "https://api.real.com/v1",
            ApiKey = "key",
            Type = "openai",
            IsActive = true,
            IsVirtual = false,
            Priority = 100,
            Models = new List<ProviderModel>
            {
                new() { ModelName = "real-model", IsEnabled = true }
            }
        };

        _context.Providers.AddRange(virtualProvider, realProvider);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetAllProvidersAsync();

        // Assert
        result.Should().HaveCount(2);
        result.Should().Contain(p => p.Name == "Virtual" && p.IsVirtual);
        result.Should().Contain(p => p.Name == "Real" && !p.IsVirtual);
    }

    [Fact]
    public async Task CreateProviderAsync_WithNullHeaders_SetsNullHeaders()
    {
        // Arrange
        var providerDto = new ProviderDto
        {
            Name = "No Headers",
            BaseUrl = "https://api.none.com/v1",
            ApiKey = "key",
            Type = "openai",
            Headers = null,
            IsActive = true,
            Priority = 50,
            Models = new List<ProviderModelDto>()
        };

        // Act
        var result = await _service.CreateProviderAsync(providerDto);

        // Assert
        result.Should().NotBeNull();
        result.Headers.Should().BeNullOrEmpty();
    }

    [Fact]
    public async Task CreateProviderAsync_WithEmptyModels_CreatesProviderWithoutModels()
    {
        // Arrange
        var providerDto = new ProviderDto
        {
            Name = "No Models",
            BaseUrl = "https://api.nomodel.com/v1",
            ApiKey = "key",
            Type = "openai",
            Headers = new Dictionary<string, string>(),
            IsActive = true,
            Priority = 50,
            Models = new List<ProviderModelDto>() // Empty list
        };

        // Act
        var result = await _service.CreateProviderAsync(providerDto);

        // Assert
        result.Should().NotBeNull();
        result.Models.Should().BeEmpty();
    }

    [Fact]
    public async Task UpdateProviderAsync_WithNullModels_RemovesAllModels()
    {
        // Arrange
        var provider = new Provider
        {
            Name = "Original",
            BaseUrl = "https://api.original.com/v1",
            ApiKey = "key",
            IsActive = true,
            Priority = 50,
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
            ApiKey = "new-key",
            Models = new List<ProviderModelDto>() // Empty list to remove all models
        };

        // Act
        var result = await _service.UpdateProviderAsync(provider.Id, updateDto);

        // Assert
        result.Should().NotBeNull();
        result!.Models.Should().BeEmpty();

        var dbModels = await _context.ProviderModels.Where(m => m.ProviderId == provider.Id).ToListAsync();
        dbModels.Should().BeEmpty();
    }

    [Fact]
    public async Task GetAvailableModelsAsync_WithRateLimitedProvider_ExcludesIt()
    {
        // Arrange
        var limitedProvider = new Provider
        {
            Name = "Limited",
            BaseUrl = "https://api.limited.com/v1",
            ApiKey = "key",
            Priority = 100,
            IsActive = true,
            RateLimitJson = "{\"RequestsPerDay\": 10, \"TokensPerDay\": 1000}",
            RequestsToday = 10, // At limit
            TokensToday = 0,
            LastResetDate = DateTime.UtcNow.Date
        };

        var availableProvider = new Provider
        {
            Name = "Available",
            BaseUrl = "https://api.available.com/v1",
            ApiKey = "key",
            Priority = 50,
            IsActive = true,
            RateLimitJson = "{\"RequestsPerDay\": 100, \"TokensPerDay\": 10000}",
            RequestsToday = 0,
            TokensToday = 0,
            LastResetDate = DateTime.UtcNow.Date
        };

        limitedProvider.Models = new List<ProviderModel>
        {
            new() { ModelName = "limited-model", IsEnabled = true }
        };
        availableProvider.Models = new List<ProviderModel>
        {
            new() { ModelName = "available-model", IsEnabled = true }
        };

        _context.Providers.AddRange(limitedProvider, availableProvider);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetAvailableModelsAsync();

        // Assert
        result.Should().HaveCount(1);
        result[0].Id.Should().Be("available-model");
    }

    [Fact]
    public async Task GetAvailableModelsAsync_WithHourlyRateLimit_ExcludesWhenLimited()
    {
        // Arrange
        var lastHour = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, DateTime.UtcNow.Day,
                                    DateTime.UtcNow.Hour, 0, 0, DateTimeKind.Utc);
        var limitedProvider = new Provider
        {
            Name = "Hourly Limited",
            BaseUrl = "https://api.hourly.com/v1",
            ApiKey = "key",
            Priority = 100,
            IsActive = true,
            RateLimitJson
                = "{\"RequestsPerDay\": 0, \"TokensPerDay\": 0, \"RequestsPerHour\": 5, \"TokensPerHour\": 1000}",
            RequestsToday = 0,
            TokensToday = 0,
            LastResetDate = DateTime.UtcNow.Date,
            RequestsThisHour = 5, // At hourly limit
            TokensThisHour = 0,
            LastHourReset = lastHour
        };

        var availableProvider = new Provider
        {
            Name = "Available",
            BaseUrl = "https://api.available.com/v1",
            ApiKey = "key",
            Priority = 50,
            IsActive = true,
            RateLimitJson = "{\"RequestsPerDay\": 100, \"TokensPerDay\": 10000}",
            RequestsToday = 0,
            TokensToday = 0,
            LastResetDate = DateTime.UtcNow.Date
        };

        limitedProvider.Models = new List<ProviderModel>
        {
            new() { ModelName = "hourly-model", IsEnabled = true }
        };
        availableProvider.Models = new List<ProviderModel>
        {
            new() { ModelName = "available-model", IsEnabled = true }
        };

        _context.Providers.AddRange(limitedProvider, availableProvider);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetAvailableModelsAsync();

        // Assert
        result.Should().HaveCount(1);
        result[0].Id.Should().Be("available-model");
    }

    [Fact]
    public async Task GetGroupedModelsAsync_FiltersRateLimitedProviders()
    {
        // Arrange
        var limitedProvider = new Provider
        {
            Name = "Rate Limited",
            BaseUrl = "https://api.rate.com/v1",
            ApiKey = "key",
            Priority = 100,
            IsActive = true,
            RateLimitJson = "{\"RequestsPerDay\": 10, \"TokensPerDay\": 1000}",
            RequestsToday = 10, // At limit
            TokensToday = 0,
            LastResetDate = DateTime.UtcNow.Date
        };

        var availableProvider = new Provider
        {
            Name = "Available",
            BaseUrl = "https://api.available.com/v1",
            ApiKey = "key",
            Priority = 50,
            IsActive = true,
            RateLimitJson = "{\"RequestsPerDay\": 100, \"TokensPerDay\": 10000}",
            RequestsToday = 0,
            TokensToday = 0,
            LastResetDate = DateTime.UtcNow.Date
        };

        limitedProvider.Models = new List<ProviderModel>
        {
            new() { ModelName = "limited-model", IsEnabled = true }
        };
        availableProvider.Models = new List<ProviderModel>
        {
            new() { ModelName = "available-model", IsEnabled = true, IsFree = true }
        };

        _context.Providers.AddRange(limitedProvider, availableProvider);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetGroupedModelsAsync();

        // Assert
        result.Groups.Should().NotBeEmpty();

        // Free models group should only have available-model
        var freeGroup = result.Groups.FirstOrDefault(g => g.Type == "free");
        freeGroup.Should().NotBeNull();
        freeGroup!.Models.Should().HaveCount(1);
        freeGroup.Models[0].Id.Should().Be("available-model");

        // Provider group for Available should exist
        var availGroup = result.Groups.FirstOrDefault(g => g.Name == "Available");
        availGroup.Should().NotBeNull();
        availGroup!.Models.Should().HaveCount(1);

        // Rate Limited group should NOT exist
        var limitedGroup = result.Groups.FirstOrDefault(g => g.Name == "Rate Limited");
        limitedGroup.Should().BeNull();
    }

    [Fact]
    public async Task FindAvailableProviderForModelAsync_WithEstimatedTokens_ChecksTokenLimit()
    {
        // Arrange
        var provider = new Provider
        {
            Name = "Token Limited",
            BaseUrl = "https://api.token.com/v1",
            ApiKey = "key",
            Priority = 100,
            IsActive = true,
            RateLimitJson = "{\"RequestsPerDay\": 100, \"TokensPerDay\": 1000}",
            RequestsToday = 0,
            TokensToday = 900, // Close to limit
            LastResetDate = DateTime.UtcNow.Date,
            Models = new List<ProviderModel>
            {
                new() { ModelName = "expensive-model", IsEnabled = true }
            }
        };

        _context.Providers.Add(provider);
        await _context.SaveChangesAsync();

        // Act - Request with estimated tokens that would exceed limit
        var result = await _service.FindAvailableProviderForModelAsync("expensive-model", 200);

        // Assert - Should return null because 900 + 200 > 1000
        result.Should().BeNull();
    }

    [Fact]
    public async Task FindAvailableProviderForModelAsync_WithEstimatedTokens_WithinLimit_ReturnsProvider()
    {
        // Arrange
        var provider = new Provider
        {
            Name = "Token OK",
            BaseUrl = "https://api.tokenok.com/v1",
            ApiKey = "key",
            Priority = 100,
            IsActive = true,
            RateLimitJson = "{\"RequestsPerDay\": 100, \"TokensPerDay\": 1000}",
            RequestsToday = 0,
            TokensToday = 500, // Half used
            LastResetDate = DateTime.UtcNow.Date,
            Models = new List<ProviderModel>
            {
                new() { ModelName = "model1", IsEnabled = true }
            }
        };

        _context.Providers.Add(provider);
        await _context.SaveChangesAsync();

        // Act - Request with estimated tokens within limit
        var result = await _service.FindAvailableProviderForModelAsync("model1", 200);

        // Assert - Should return provider because 500 + 200 <= 1000
        result.Should().NotBeNull();
        result!.Name.Should().Be("Token OK");
    }

    [Fact]
    public async Task GetAllAvailableProvidersForModelAsync_WithEstimatedTokens_FiltersByTokenLimit()
    {
        // Arrange
        var provider1 = new Provider
        {
            Name = "High Tokens Used",
            BaseUrl = "https://api.high.com/v1",
            ApiKey = "key1",
            Priority = 100,
            IsActive = true,
            RateLimitJson = "{\"RequestsPerDay\": 100, \"TokensPerDay\": 1000}",
            RequestsToday = 0,
            TokensToday = 900,
            LastResetDate = DateTime.UtcNow.Date
        };

        var provider2 = new Provider
        {
            Name = "Low Tokens Used",
            BaseUrl = "https://api.low.com/v1",
            ApiKey = "key2",
            Priority = 50,
            IsActive = true,
            RateLimitJson = "{\"RequestsPerDay\": 100, \"TokensPerDay\": 1000}",
            RequestsToday = 0,
            TokensToday = 100,
            LastResetDate = DateTime.UtcNow.Date
        };

        provider1.Models = new List<ProviderModel> { new() { ModelName = "shared", IsEnabled = true } };
        provider2.Models = new List<ProviderModel> { new() { ModelName = "shared", IsEnabled = true } };

        _context.Providers.AddRange(provider1, provider2);
        await _context.SaveChangesAsync();

        // Act - Request with 200 estimated tokens
        var result = await _service.GetAllAvailableProvidersForModelAsync("shared", 200);

        // Assert - Only provider2 should be available (100 + 200 <= 1000)
        result.Should().HaveCount(1);
        result[0].Name.Should().Be("Low Tokens Used");
    }

    [Fact]
    public async Task FindAvailableProviderForModelAsync_WithHourlyTokenLimit_FiltersCorrectly()
    {
        // Arrange
        var lastHour = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, DateTime.UtcNow.Day,
                                    DateTime.UtcNow.Hour, 0, 0, DateTimeKind.Utc);
        var provider = new Provider
        {
            Name = "Hourly Token Limited",
            BaseUrl = "https://api.hourly.com/v1",
            ApiKey = "key",
            Priority = 100,
            IsActive = true,
            RateLimitJson
                = "{\"RequestsPerDay\": 0, \"TokensPerDay\": 0, \"RequestsPerHour\": 100, \"TokensPerHour\": 500}",
            RequestsToday = 0,
            TokensToday = 0,
            LastResetDate = DateTime.UtcNow.Date,
            RequestsThisHour = 0,
            TokensThisHour = 450, // Close to hourly limit
            LastHourReset = lastHour,
            Models = new List<ProviderModel>
            {
                new() { ModelName = "model1", IsEnabled = true }
            }
        };

        _context.Providers.Add(provider);
        await _context.SaveChangesAsync();

        // Act - Request with 100 estimated tokens (would exceed 500 hourly)
        var result = await _service.FindAvailableProviderForModelAsync("model1", 100);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task RecordUsageAsync_WithZeroTokens_IncrementsOnlyRequests()
    {
        // Arrange
        var provider = new Provider
        {
            Name = "Test",
            BaseUrl = "https://api.test.com/v1",
            ApiKey = "key",
            Priority = 100,
            IsActive = true,
            RateLimitJson = "{\"RequestsPerDay\": 100, \"TokensPerDay\": 1000}",
            RequestsToday = 5,
            TokensToday = 500,
            LastResetDate = DateTime.UtcNow.Date
        };

        _context.Providers.Add(provider);
        await _context.SaveChangesAsync();

        // Act
        await _service.RecordUsageAsync(provider.Id, 0);

        // Assert
        var updated = await _context.Providers.FindAsync(provider.Id);
        updated!.RequestsToday.Should().Be(6);
        updated.TokensToday.Should().Be(500); // Unchanged
    }
}