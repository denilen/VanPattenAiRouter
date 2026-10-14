using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VanPattenAIRouter.Data;
using VanPattenAIRouter.Services;
using Xunit;

namespace VanPattenAIRouter.Tests.Services;

public class ProviderServiceExtendedTests : IDisposable
{
    private readonly AiRouterDbContext _context;
    private readonly ILogger<ProviderService> _logger;
    private readonly ProviderService _service;

    public ProviderServiceExtendedTests()
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
    public async Task GetAvailableModelsAsync_FiltersByRateLimit_Daily()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var today = now.Date;
        var thisHour = new DateTime(now.Year, now.Month, now.Day, now.Hour, 0, 0, DateTimeKind.Utc);

        var rateLimitedProvider = new Provider
        {
            Name = "Rate Limited",
            BaseUrl = "https://api.rate.com/v1",
            ApiKey = "key",
            Priority = 100,
            IsActive = true,
            RateLimitJson = "{\"RequestsPerDay\": 5, \"TokensPerDay\": 1000}",
            RequestsToday = 5, // At limit
            TokensToday = 0,
            LastResetDate = today,
            LastHourReset = thisHour
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
            LastResetDate = today,
            LastHourReset = thisHour
        };

        rateLimitedProvider.Models = new List<ProviderModel>
        {
            new() { ModelName = "model-1", IsEnabled = true }
        };
        availableProvider.Models = new List<ProviderModel>
        {
            new() { ModelName = "model-2", IsEnabled = true }
        };

        _context.Providers.AddRange(rateLimitedProvider, availableProvider);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetAvailableModelsAsync();

        // Assert
        result.Should().HaveCount(1);
        result[0].Id.Should().Be("model-2");
        result[0].OwnedBy.Should().Be("Available");
    }

    [Fact]
    public async Task GetAvailableModelsAsync_FiltersByRateLimit_Hourly()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var today = now.Date;
        var thisHour = new DateTime(now.Year, now.Month, now.Day, now.Hour, 0, 0, DateTimeKind.Utc);

        var rateLimitedProvider = new Provider
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
            LastResetDate = today,
            RequestsThisHour = 5, // At hourly limit
            TokensThisHour = 0,
            LastHourReset = thisHour
        };

        var availableProvider = new Provider
        {
            Name = "Available",
            BaseUrl = "https://api.available.com/v1",
            ApiKey = "key",
            Priority = 50,
            IsActive = true,
            RateLimitJson
                = "{\"RequestsPerDay\": 100, \"TokensPerDay\": 10000, \"RequestsPerHour\": 0, \"TokensPerHour\": 0}",
            RequestsToday = 0,
            TokensToday = 0,
            LastResetDate = today,
            LastHourReset = thisHour
        };

        rateLimitedProvider.Models = new List<ProviderModel>
        {
            new() { ModelName = "model-1", IsEnabled = true }
        };
        availableProvider.Models = new List<ProviderModel>
        {
            new() { ModelName = "model-2", IsEnabled = true }
        };

        _context.Providers.AddRange(rateLimitedProvider, availableProvider);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetAvailableModelsAsync();

        // Assert
        result.Should().HaveCount(1);
        result[0].Id.Should().Be("model-2");
    }

    [Fact]
    public async Task GetGroupedModelsAsync_ReturnsCorrectGroups()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var today = now.Date;
        var thisHour = new DateTime(now.Year, now.Month, now.Day, now.Hour, 0, 0, DateTimeKind.Utc);

        var provider1 = new Provider
        {
            Name = "Provider A",
            BaseUrl = "https://api.a.com/v1",
            ApiKey = "key",
            Priority = 100,
            IsActive = true,
            LastResetDate = today,
            LastHourReset = thisHour,
            Models = new List<ProviderModel>
            {
                new() { ModelName = "free-model", IsEnabled = true, IsFree = true },
                new() { ModelName = "paid-model", IsEnabled = true, IsFree = false }
            }
        };

        var provider2 = new Provider
        {
            Name = "Provider B",
            BaseUrl = "https://api.b.com/v1",
            ApiKey = "key",
            Priority = 50,
            IsActive = true,
            LastResetDate = today,
            LastHourReset = thisHour,
            Models = new List<ProviderModel>
            {
                new() { ModelName = "free-model", IsEnabled = true, IsFree = true },
                new() { ModelName = "another-free", IsEnabled = true, IsFree = true }
            }
        };

        _context.Providers.AddRange(provider1, provider2);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetGroupedModelsAsync();

        // Assert
        result.Groups.Should().NotBeEmpty();

        // Should have Free Models group
        var freeGroup = result.Groups.FirstOrDefault(g => g.Type == "free");
        freeGroup.Should().NotBeNull();
        freeGroup!.Models.Should().HaveCount(2); // free-model (deduped from both), another-free

        // Should have provider groups
        var providerAGroup = result.Groups.FirstOrDefault(g => g.Name == "Provider A");
        providerAGroup.Should().NotBeNull();
        providerAGroup!.Models.Should().HaveCount(2);

        var providerBGroup = result.Groups.FirstOrDefault(g => g.Name == "Provider B");
        providerBGroup.Should().NotBeNull();
        providerBGroup!.Models.Should().HaveCount(2);
    }

    [Fact]
    public async Task FindProviderForModelAsync_HandlesSuffixMatching()
    {
        // Arrange - Provider has full model name, request uses suffix
        var provider = new Provider
        {
            Name = "OpenAI",
            BaseUrl = "https://api.openai.com/v1",
            ApiKey = "key",
            Priority = 100,
            IsActive = true,
            RateLimitJson = "{\"RequestsPerDay\": 100, \"TokensPerDay\": 10000}",
            RequestsToday = 0,
            TokensToday = 0,
            LastResetDate = DateTime.UtcNow.Date,
            Models = new List<ProviderModel>
            {
                new() { ModelName = "openai/gpt-4o-mini", ProviderModelName = "gpt-4o-mini", IsEnabled = true }
            }
        };

        _context.Providers.Add(provider);
        await _context.SaveChangesAsync();

        // Act - Request with suffix
        var result = await _service.FindProviderForModelAsync("gpt-4o-mini");

        // Assert
        result.Should().NotBeNull();
        result!.Name.Should().Be("OpenAI");
    }

    [Fact]
    public async Task GetAllAvailableProvidersForModelAsync_ReturnsMultipleProviders()
    {
        // Arrange
        var provider1 = new Provider
        {
            Name = "High Priority",
            BaseUrl = "https://api.high.com/v1",
            ApiKey = "key1",
            Priority = 100,
            IsActive = true,
            RateLimitJson = "{\"RequestsPerDay\": 100, \"TokensPerDay\": 10000}",
            RequestsToday = 0,
            TokensToday = 0,
            LastResetDate = DateTime.UtcNow.Date,
            Models = new List<ProviderModel>
            {
                new() { ModelName = "shared-model", IsEnabled = true }
            }
        };

        var provider2 = new Provider
        {
            Name = "Low Priority",
            BaseUrl = "https://api.low.com/v1",
            ApiKey = "key2",
            Priority = 50,
            IsActive = true,
            RateLimitJson = "{\"RequestsPerDay\": 100, \"TokensPerDay\": 10000}",
            RequestsToday = 0,
            TokensToday = 0,
            LastResetDate = DateTime.UtcNow.Date,
            Models = new List<ProviderModel>
            {
                new() { ModelName = "shared-model", IsEnabled = true }
            }
        };

        _context.Providers.AddRange(provider1, provider2);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetAllAvailableProvidersForModelAsync("shared-model");

        // Assert
        result.Should().HaveCount(2);
        result[0].Name.Should().Be("High Priority");
        result[1].Name.Should().Be("Low Priority");
    }

    [Fact]
    public async Task RecordUsageAsync_ResetsDailyCounters_WhenNewDay()
    {
        // Arrange
        var provider = new Provider
        {
            Name = "Test",
            BaseUrl = "https://api.test.com/v1",
            ApiKey = "key",
            Priority = 100,
            IsActive = true,
            RateLimitJson = "{\"RequestsPerDay\": 100, \"TokensPerDay\": 10000}",
            RequestsToday = 50,
            TokensToday = 5000,
            LastResetDate = DateTime.UtcNow.Date.AddDays(-1) // Yesterday
        };

        _context.Providers.Add(provider);
        await _context.SaveChangesAsync();

        // Act
        await _service.RecordUsageAsync(provider.Id, 100);

        // Assert
        var updated = await _context.Providers.FindAsync(provider.Id);
        updated!.RequestsToday.Should().Be(1); // Reset to 0 then +1
        updated.TokensToday.Should().Be(100);  // Reset to 0 then +100
    }

    [Fact]
    public async Task RecordUsageAsync_ResetsHourlyCounters_WhenNewHour()
    {
        // Arrange
        var lastHour = DateTime.UtcNow.AddHours(-1);
        var provider = new Provider
        {
            Name = "Test",
            BaseUrl = "https://api.test.com/v1",
            ApiKey = "key",
            Priority = 100,
            IsActive = true,
            RateLimitJson
                = "{\"RequestsPerDay\": 0, \"TokensPerDay\": 0, \"RequestsPerHour\": 100, \"TokensPerHour\": 10000}",
            RequestsToday = 10,
            TokensToday = 1000,
            LastResetDate = DateTime.UtcNow.Date,
            RequestsThisHour = 50,
            TokensThisHour = 5000,
            LastHourReset = lastHour // Last hour
        };

        _context.Providers.Add(provider);
        await _context.SaveChangesAsync();

        // Act
        await _service.RecordUsageAsync(provider.Id, 100);

        // Assert
        var updated = await _context.Providers.FindAsync(provider.Id);
        updated!.RequestsThisHour.Should().Be(1); // Reset to 0 then +1
        updated.TokensThisHour.Should().Be(100);  // Reset to 0 then +100
    }

    [Fact]
    public async Task ResetDailyUsageIfNeededAsync_ResetsBothDailyAndHourly()
    {
        // Arrange
        var lastHour = DateTime.UtcNow.AddHours(-1);
        var provider = new Provider
        {
            Name = "Test",
            BaseUrl = "https://api.test.com/v1",
            ApiKey = "key",
            Priority = 100,
            IsActive = true,
            RateLimitJson
                = "{\"RequestsPerDay\": 100, \"TokensPerDay\": 10000, \"RequestsPerHour\": 50, \"TokensPerHour\": 5000}",
            RequestsToday = 100,
            TokensToday = 10000,
            LastResetDate = DateTime.UtcNow.Date.AddDays(-1), // Yesterday
            RequestsThisHour = 50,
            TokensThisHour = 5000,
            LastHourReset = lastHour // Last hour
        };

        _context.Providers.Add(provider);
        await _context.SaveChangesAsync();

        // Act
        await _service.ResetDailyUsageIfNeededAsync();

        // Assert
        var updated = await _context.Providers.FindAsync(provider.Id);
        updated!.RequestsToday.Should().Be(0);
        updated.TokensToday.Should().Be(0);
        updated.RequestsThisHour.Should().Be(0);
        updated.TokensThisHour.Should().Be(0);
    }

    [Fact]
    public async Task GetAvailableModelsAsync_HandlesVirtualProvider()
    {
        // Arrange
        var virtualProvider = new Provider
        {
            Name = "Virtual",
            BaseUrl = "",
            ApiKey = "",
            Priority = 200,
            IsActive = true,
            IsVirtual = true,
            RateLimitJson = "{\"RequestsPerDay\": 10000, \"TokensPerDay\": 1000000}",
            RequestsToday = 0,
            TokensToday = 0,
            LastResetDate = DateTime.UtcNow.Date,
            Models = new List<ProviderModel>
            {
                new() { ModelName = "auto-free", IsEnabled = true, IsFree = true },
                new() { ModelName = "auto-all", IsEnabled = true, IsFree = false }
            }
        };

        _context.Providers.Add(virtualProvider);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetAvailableModelsAsync();

        // Assert - Virtual provider models should be included
        result.Should().HaveCount(2);
        result.Should().Contain(m => m.Id == "auto-free" && m.OwnedBy == "Virtual");
        result.Should().Contain(m => m.Id == "auto-all" && m.OwnedBy == "Virtual");
    }

    [Fact]
    public async Task GetProviderByIdAsync_IncludesInactiveProviders()
    {
        // Arrange
        var provider = new Provider
        {
            Name = "Inactive Provider",
            BaseUrl = "https://api.inactive.com/v1",
            ApiKey = "key",
            Priority = 100,
            IsActive = false,
            Models = new List<ProviderModel>
            {
                new() { ModelName = "model1", IsEnabled = true }
            }
        };

        _context.Providers.Add(provider);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetProviderByIdAsync(provider.Id);

        // Assert - Should return provider even if inactive
        result.Should().NotBeNull();
        result!.Name.Should().Be("Inactive Provider");
        result.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task FindProviderForModelAsync_SkipsInactiveModels()
    {
        // Arrange
        var provider = new Provider
        {
            Name = "Test",
            BaseUrl = "https://api.test.com/v1",
            ApiKey = "key",
            Priority = 100,
            IsActive = true,
            RateLimitJson = "{\"RequestsPerDay\": 100, \"TokensPerDay\": 10000}",
            RequestsToday = 0,
            TokensToday = 0,
            LastResetDate = DateTime.UtcNow.Date,
            Models = new List<ProviderModel>
            {
                new() { ModelName = "enabled-model", IsEnabled = true },
                new() { ModelName = "disabled-model", IsEnabled = false }
            }
        };

        _context.Providers.Add(provider);
        await _context.SaveChangesAsync();

        // Act - Should find enabled model
        var result1 = await _service.FindProviderForModelAsync("enabled-model");

        // Act - Should not find disabled model
        var result2 = await _service.FindProviderForModelAsync("disabled-model");

        // Assert
        result1.Should().NotBeNull();
        result2.Should().BeNull();
    }
}