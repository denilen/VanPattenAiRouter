using System.Reflection;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using VanPattenAIRouter.Data;
using VanPattenAIRouter.Models;
using Xunit;

namespace VanPattenAIRouter.Tests.Data;

public class SeedDataExtendedTests : IDisposable
{
    private readonly AiRouterDbContext _context;

    public SeedDataExtendedTests()
    {
        var options = new DbContextOptionsBuilder<AiRouterDbContext>()
                     .UseInMemoryDatabase(Guid.NewGuid().ToString())
                     .Options;

        _context = new AiRouterDbContext(options);
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    private static IOptions<AppOptions> CreateAppOptions(Dictionary<string, string?> configDict)
    {
        var configuration = new ConfigurationBuilder()
                           .AddInMemoryCollection(configDict)
                           .Build();
        var appOptions = new AppOptions
        {
            Router = configuration.GetSection("Router").Get<RouterOptions>() ?? new RouterOptions()
        };
        return Options.Create(appOptions);
    }

    [Fact]
    public async Task InitializeAsync_DoesNothing_WhenDatabaseAlreadySeeded()
    {
        // Arrange
        var existingProvider = new Provider
        {
            Name = "Existing",
            BaseUrl = "https://api.existing.com/v1",
            ApiKey = "key",
            IsActive = true
        };
        _context.Providers.Add(existingProvider);
        await _context.SaveChangesAsync();

        var appOptions = CreateAppOptions(new Dictionary<string, string?>
        {
            ["Router:ModelRefreshIntervalMinutes"] = "10"
        });

        // Act
        await SeedData.InitializeAsync(_context, appOptions);

        // Assert - Should not add more providers
        var count = await _context.Providers.CountAsync();
        count.Should().Be(1);
    }

    [Fact]
    public async Task InitializeAsync_CreatesProviders_FromConfiguration()
    {
        // Arrange
        var appOptions = CreateAppOptions(new Dictionary<string, string?>
        {
            ["Router:ModelRefreshIntervalMinutes"] = "10",
            ["Router:Providers:0:Name"] = "Test Provider 1",
            ["Router:Providers:0:BaseUrl"] = "https://api.test1.com/v1",
            ["Router:Providers:0:ApiKey"] = "key1",
            ["Router:Providers:0:Type"] = "openai",
            ["Router:Providers:0:IsActive"] = "true",
            ["Router:Providers:0:Priority"] = "100",
            ["Router:Providers:0:IsVirtual"] = "false",
            ["Router:Providers:0:RateLimit:RequestsPerDay"] = "1000",
            ["Router:Providers:0:RateLimit:TokensPerDay"] = "100000",
            ["Router:Providers:1:Name"] = "Test Provider 2",
            ["Router:Providers:1:BaseUrl"] = "https://api.test2.com/v1",
            ["Router:Providers:1:ApiKey"] = "key2",
            ["Router:Providers:1:Type"] = "anthropic",
            ["Router:Providers:1:IsActive"] = "false",
            ["Router:Providers:1:Priority"] = "50",
            ["Router:Providers:1:IsVirtual"] = "false",
            ["Router:Providers:1:RateLimit:RequestsPerDay"] = "500",
            ["Router:Providers:1:RateLimit:TokensPerDay"] = "50000"
        });

        // Act
        await SeedData.InitializeAsync(_context, appOptions);

        // Assert
        var providers = await _context.Providers.ToListAsync();
        providers.Should().HaveCount(2);
        providers.Should().Contain(p => p.Name == "Test Provider 1" && p.Priority == 100);
        providers.Should().Contain(p => p.Name == "Test Provider 2" && p.Priority == 50);
    }

    [Fact]
    public async Task InitializeAsync_CreatesVirtualProviderWithModels()
    {
        // Arrange
        var appOptions = CreateAppOptions(new Dictionary<string, string?>
        {
            ["Router:ModelRefreshIntervalMinutes"] = "10",
            ["Router:Providers:0:Name"] = "Virtual Provider",
            ["Router:Providers:0:BaseUrl"] = "",
            ["Router:Providers:0:ApiKey"] = "",
            ["Router:Providers:0:Type"] = "virtual",
            ["Router:Providers:0:IsActive"] = "true",
            ["Router:Providers:0:Priority"] = "200",
            ["Router:Providers:0:IsVirtual"] = "true",
            ["Router:Providers:0:RateLimit:RequestsPerDay"] = "10000",
            ["Router:Providers:0:RateLimit:TokensPerDay"] = "1000000",
            ["Router:Providers:0:Models:0:Name"] = "auto-free",
            ["Router:Providers:0:Models:0:Description"] = "Auto free model",
            ["Router:Providers:0:Models:0:Mode"] = "auto-free",
            ["Router:Providers:0:Models:0:IsEnabled"] = "true",
            ["Router:Providers:0:Models:1:Name"] = "auto-all",
            ["Router:Providers:0:Models:1:Description"] = "Auto all model",
            ["Router:Providers:0:Models:1:Mode"] = "auto-all",
            ["Router:Providers:0:Models:1:IsEnabled"] = "true",
            ["Router:Providers:0:Models:2:Name"] = "disabled-model",
            ["Router:Providers:0:Models:2:Description"] = "Disabled",
            ["Router:Providers:0:Models:2:Mode"] = "normal",
            ["Router:Providers:0:Models:2:IsEnabled"] = "false"
        });

        // Act
        await SeedData.InitializeAsync(_context, appOptions);

        // Assert
        var provider = await _context.Providers.FirstOrDefaultAsync(p => p.Name == "Virtual Provider");
        provider.Should().NotBeNull();
        provider!.IsVirtual.Should().BeTrue();

        var models = await _context.ProviderModels.Where(m => m.ProviderId == provider.Id).ToListAsync();
        models.Should().HaveCount(2); // Only enabled models
        models.Should().Contain(m => m.ModelName == "auto-free" && m.IsFree == true);
        models.Should().Contain(m => m.ModelName == "auto-all" && m.IsFree == false);
        models.Should().NotContain(m => m.ModelName == "disabled-model");
    }

    [Fact]
    public async Task InitializeAsync_SerializesHeadersAndRateLimitCorrectly()
    {
        // Arrange
        var appOptions = CreateAppOptions(new Dictionary<string, string?>
        {
            ["Router:ModelRefreshIntervalMinutes"] = "10",
            ["Router:Providers:0:Name"] = "Header Provider",
            ["Router:Providers:0:BaseUrl"] = "https://api.header.com/v1",
            ["Router:Providers:0:ApiKey"] = "key",
            ["Router:Providers:0:Type"] = "openai",
            ["Router:Providers:0:IsActive"] = "true",
            ["Router:Providers:0:Priority"] = "100",
            ["Router:Providers:0:Headers:Custom-Header"] = "value1",
            ["Router:Providers:0:Headers:Another-Header"] = "value2",
            ["Router:Providers:0:RateLimit:RequestsPerDay"] = "500",
            ["Router:Providers:0:RateLimit:TokensPerDay"] = "50000",
            ["Router:Providers:0:RateLimit:RequestsPerHour"] = "100",
            ["Router:Providers:0:RateLimit:TokensPerHour"] = "5000"
        });

        // Act
        await SeedData.InitializeAsync(_context, appOptions);

        // Assert
        var provider = await _context.Providers.FirstOrDefaultAsync(p => p.Name == "Header Provider");
        provider.Should().NotBeNull();
        provider!.Headers.Should().NotBeNullOrEmpty();
        provider.Headers.Should().Contain("Custom-Header");
        provider.Headers.Should().Contain("value1");

        var rateLimit = provider.RateLimit;
        rateLimit.RequestsPerDay.Should().Be(500);
        rateLimit.TokensPerDay.Should().Be(50000);
        rateLimit.RequestsPerHour.Should().Be(100);
        rateLimit.TokensPerHour.Should().Be(5000);
    }

    [Fact]
    public async Task InitializeAsync_FallsBackToDefaults_WhenNoConfigProviders()
    {
        // Arrange - Config without Providers section
        var appOptions = CreateAppOptions(new Dictionary<string, string?>
        {
            ["Router:ModelRefreshIntervalMinutes"] = "10"
        });

        // Act
        await SeedData.InitializeAsync(_context, appOptions);

        // Assert - Should create default OpenAI provider
        var providers = await _context.Providers.ToListAsync();
        providers.Should().HaveCount(1);
        providers[0].Name.Should().Be("OpenAI");
        providers[0].BaseUrl.Should().Be("https://api.openai.com/v1");
    }

    [Fact]
    public async Task InitializeAsync_HandlesNullConfiguration()
    {
        // Act
        await SeedData.InitializeAsync(_context, Options.Create<AppOptions>(null!));

        // Assert - Should create default provider
        var providers = await _context.Providers.ToListAsync();
        providers.Should().HaveCount(1);
        providers[0].Name.Should().Be("OpenAI");
    }

    [Fact]
    public void ResolveApiKey_ReturnsEmptyString_ForNullOrEmpty()
    {
        // Act & Assert
        var result1 = GetResolveApiKey(null);
        result1.Should().Be("");

        var result2 = GetResolveApiKey("");
        result2.Should().Be("");

        var result3 = GetResolveApiKey("   ");
        result3.Should().Be("");
    }

    [Fact]
    public void ResolveApiKey_ReturnsValue_ForDirectKey()
    {
        // Act
        var result = GetResolveApiKey("sk-direct-key-123");

        // Assert
        result.Should().Be("sk-direct-key-123");
    }

    [Fact]
    public void ResolveApiKey_ResolvesEnvironmentVariable()
    {
        // Arrange
        Environment.SetEnvironmentVariable("TEST_API_KEY", "env-key-value");

        // Act
        var result = GetResolveApiKey("${TEST_API_KEY}");

        // Assert
        result.Should().Be("env-key-value");

        // Cleanup
        Environment.SetEnvironmentVariable("TEST_API_KEY", null);
    }

    [Fact]
    public void ResolveApiKey_ReturnsEmpty_ForMissingEnvironmentVariable()
    {
        // Arrange - Ensure env var doesn't exist
        Environment.SetEnvironmentVariable("MISSING_KEY", null);

        // Act
        var result = GetResolveApiKey("${MISSING_KEY}");

        // Assert
        result.Should().Be("");
    }

    [Fact]
    public void ResolveApiKey_HandlesNonEnvVarFormat()
    {
        // Act
        var result1 = GetResolveApiKey("not-an-env-var");
        var result2 = GetResolveApiKey("${INCOMPLETE");
        var result3 = GetResolveApiKey("INCOMPLETE}");

        // Assert - Should return as-is if not in ${VAR} format
        result1.Should().Be("not-an-env-var");
        result2.Should().Be("${INCOMPLETE");
        result3.Should().Be("INCOMPLETE}");
    }

    private static string GetResolveApiKey(string? apiKey)
    {
        var method = typeof(SeedData).GetMethod("ResolveApiKey",
                                                BindingFlags.NonPublic | BindingFlags.Static);
#pragma warning disable CS8601
        var result = method!.Invoke(null, new object[] { apiKey });
        return (string)result!;
#pragma warning restore CS8601
    }
}