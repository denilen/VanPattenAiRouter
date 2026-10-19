using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using VanPattenAIRouter.Data;
using VanPattenAIRouter.Models;
using VanPattenAIRouter.Services;
using Xunit;

namespace VanPattenAIRouter.Tests.Services;

/// <summary>
///     Comprehensive tests for rate limit handling and provider routing scenarios.
/// </summary>
public class RateLimitAndRoutingScenarioTests
{
    private readonly AiRouterDbContext _context;
    private readonly ProviderService _providerService;

    public RateLimitAndRoutingScenarioTests()
    {
        var options = new DbContextOptionsBuilder<AiRouterDbContext>()
                     .UseInMemoryDatabase($"RateLimitTest_{Guid.NewGuid()}")
                     .Options;
        _context = new AiRouterDbContext(options);

        var logger = new Mock<ILogger<ProviderService>>().Object;
        _providerService = new ProviderService(_context, logger);
    }

    [Fact]
    public async Task RateLimit_AtomicIncrement_PreventsRaceCondition()
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
            Priority = 100,
            RateLimitJson = JsonSerializer.Serialize(new RateLimitConfig
            {
                RequestsPerDay = 100,
                TokensPerDay = 10000
            })
        };

        _context.Providers.Add(provider);
        await _context.SaveChangesAsync();

        // Act - Simulate concurrent requests
        var tasks = new List<Task>();
        for (var i = 0; i < 50; i++) tasks.Add(_providerService.RecordUsageAsync(provider.Id, 10));
        await Task.WhenAll(tasks);

        // Assert - All 50 increments should be recorded atomically
        var updatedProvider = await _context.Providers.FindAsync(provider.Id);
        Assert.Equal(50, updatedProvider.RequestsToday);
        Assert.Equal(500, updatedProvider.TokensToday);
    }

    [Fact]
    public async Task RateLimit_DailyReset_HappensAtomically()
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
            Priority = 100,
            RequestsToday = 99,
            TokensToday = 9900,
            LastResetDate = DateTime.UtcNow.Date.AddDays(-1), // Yesterday
            RateLimitJson = JsonSerializer.Serialize(new RateLimitConfig
            {
                RequestsPerDay = 100,
                TokensPerDay = 10000
            })
        };

        _context.Providers.Add(provider);
        await _context.SaveChangesAsync();

        // Act - Reset should happen on next RecordUsageAsync
        await _providerService.RecordUsageAsync(provider.Id, 10);

        // Assert - Counters should be reset to 1, 10 (not 100, 10000)
        var updatedProvider = await _context.Providers.FindAsync(provider.Id);
        Assert.Equal(DateTime.UtcNow.Date, updatedProvider.LastResetDate);
        Assert.Equal(1, updatedProvider.RequestsToday);
        Assert.Equal(10, updatedProvider.TokensToday);
    }

    [Fact]
    public async Task RateLimit_HourlyReset_WorksIndependentlyFromDaily()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var lastHour = new DateTime(now.Year, now.Month, now.Day, now.Hour - 1, 0, 0, DateTimeKind.Utc);

        var provider = new Provider
        {
            Name = "Test Provider",
            BaseUrl = "https://api.test.com/v1",
            ApiKey = "test-key",
            Type = "openai",
            IsActive = true,
            IsVirtual = false,
            Priority = 100,
            RequestsToday = 10,
            TokensToday = 1000,
            LastResetDate = DateTime.UtcNow.Date,
            RequestsThisHour = 50,
            TokensThisHour = 5000,
            LastHourReset = lastHour, // Previous hour
            RateLimitJson = JsonSerializer.Serialize(new RateLimitConfig
            {
                RequestsPerHour = 100,
                TokensPerHour = 10000
            })
        };

        _context.Providers.Add(provider);
        await _context.SaveChangesAsync();

        // Act
        await _providerService.RecordUsageAsync(provider.Id, 10);

        // Assert - Hourly counters reset, daily preserved
        var updatedProvider = await _context.Providers.FindAsync(provider.Id);
        Assert.Equal(11, updatedProvider.RequestsToday);   // Daily preserved
        Assert.Equal(1010, updatedProvider.TokensToday);   // Daily preserved
        Assert.Equal(1, updatedProvider.RequestsThisHour); // Hourly reset + 1
        Assert.Equal(10, updatedProvider.TokensThisHour);  // Hourly reset + 10
    }

    [Fact]
    public async Task VirtualProvider_SpecificMode_DoesNotRequireIsFree()
    {
        // Arrange - Create provider with both free and paid versions of same model
        var freeProvider = new Provider
        {
            Name = "Free KiloCode",
            BaseUrl = "https://api.free.com/v1",
            ApiKey = "free-key",
            Type = "openai",
            IsActive = true,
            IsVirtual = false,
            Priority = 110,
            Models = new List<ProviderModel>
            {
                new()
                {
                    ModelName = "google/gemini-2.0-flash-exp:free",
                    ProviderModelName = "google/gemini-2.0-flash-exp:free", IsEnabled = true, IsFree = true
                }
            }
        };

        var paidProvider = new Provider
        {
            Name = "Paid KiloCode",
            BaseUrl = "https://api.paid.com/v1",
            ApiKey = "paid-key",
            Type = "openai",
            IsActive = true,
            IsVirtual = false,
            Priority = 100,
            Models = new List<ProviderModel>
            {
                new()
                {
                    ModelName = "google/gemini-2.0-flash-exp:free",
                    ProviderModelName = "google/gemini-2.0-flash-exp:free", IsEnabled = true, IsFree = false
                } // Same model, not free
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

        _context.Providers.AddRange(freeProvider, paidProvider, virtualProvider);
        await _context.SaveChangesAsync();

        // Act - Request specific model
        var (provider, mode, specificModel)
            = await _providerService.ResolveAutoProviderAsync("VanPatten/google/gemini-2.0-flash-exp:free", 100);

        // Assert - Should resolve to virtual provider with specific mode
        Assert.NotNull(provider);
        Assert.Equal("VanPatten", provider.Name);
        Assert.Equal("specific", mode);
        Assert.Equal("google/gemini-2.0-flash-exp:free", specificModel);

        // Act - Get providers for specific model (should include both free and paid)
        var providers = await _providerService.GetProvidersForSpecificModelAsync("google/gemini-2.0-flash-exp:free");

        // Assert - Both providers should be found (free and paid versions)
        Assert.Equal(2, providers.Count);
        Assert.Contains(providers, p => p.Provider.Name == "Free KiloCode" && p.IsFree);
        Assert.Contains(providers, p => p.Provider.Name == "Paid KiloCode" && !p.IsFree);
    }

    [Fact]
    public async Task VirtualProvider_SpecificMode_TriesAllProvidersWithSameModel()
    {
        // Arrange - Multiple providers with same model, different priorities
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
            Priority = 110, // Higher priority
            Models = new List<ProviderModel>
            {
                new() { ModelName = "test/model", ProviderModelName = "test/model", IsEnabled = true, IsFree = true }
            }
        };

        var provider3 = new Provider
        {
            Name = "Provider 3",
            BaseUrl = "https://api.p3.com/v1",
            ApiKey = "key3",
            Type = "openai",
            IsActive = true,
            IsVirtual = false,
            Priority = 90, // Lower priority
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

        _context.Providers.AddRange(provider1, provider2, provider3, virtualProvider);
        await _context.SaveChangesAsync();

        // Act - Get providers for specific model
        var providers = await _providerService.GetProvidersForSpecificModelAsync("test/model");

        // Assert - Should be ordered by priority (highest first)
        Assert.Equal(3, providers.Count);
        Assert.Equal("Provider 2", providers[0].Provider.Name); // Priority 110
        Assert.Equal("Provider 1", providers[1].Provider.Name); // Priority 100
        Assert.Equal("Provider 3", providers[2].Provider.Name); // Priority 90
    }
}