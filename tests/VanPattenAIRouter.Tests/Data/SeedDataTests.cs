using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using VanPattenAIRouter.Data;
using VanPattenAIRouter.Models;
using Xunit;

namespace VanPattenAIRouter.Tests.Data;

public class SeedDataTests : IDisposable
{
    private readonly IOptions<AppOptions> _appOptions;
    private readonly AiRouterDbContext _context;

    public SeedDataTests()
    {
        var options = new DbContextOptionsBuilder<AiRouterDbContext>()
                     .UseInMemoryDatabase(Guid.NewGuid().ToString())
                     .Options;

        _context = new AiRouterDbContext(options);

        // Build test configuration with RateLimit matching AppOptions structure (Router:Providers)
        var configBuilder = new ConfigurationBuilder();
        configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Router:Providers:0:Name"] = "OpenAI",
            ["Router:Providers:0:BaseUrl"] = "https://api.openai.com/v1",
            ["Router:Providers:0:ApiKey"] = "${OPENAI_API_KEY}",
            ["Router:Providers:0:Type"] = "openai",
            ["Router:Providers:0:IsActive"] = "true",
            ["Router:Providers:0:Priority"] = "100",
            ["Router:Providers:0:RateLimit:RequestsPerDay"] = "500",
            ["Router:Providers:0:RateLimit:TokensPerDay"] = "100000",
            ["Router:Providers:1:Name"] = "Anthropic",
            ["Router:Providers:1:BaseUrl"] = "https://api.anthropic.com/v1",
            ["Router:Providers:1:ApiKey"] = "${ANTHROPIC_API_KEY}",
            ["Router:Providers:1:Type"] = "anthropic",
            ["Router:Providers:1:Headers:anthropic-version"] = "2023-06-01",
            ["Router:Providers:1:IsActive"] = "true",
            ["Router:Providers:1:Priority"] = "90",
            ["Router:Providers:1:RateLimit:RequestsPerDay"] = "200",
            ["Router:Providers:1:RateLimit:TokensPerDay"] = "50000"
        });
        var configuration = configBuilder.Build();

        // Create AppOptions from configuration
        var appOptions = new AppOptions
        {
            Router = configuration.GetSection("Router").Get<RouterOptions>() ?? new RouterOptions()
        };
        _appOptions = Options.Create(appOptions);
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    [Fact]
    public async Task InitializeAsync_SeedsProvidersFromConfig_WhenDatabaseEmpty()
    {
        // Act
        await SeedData.InitializeAsync(_context, _appOptions);

        // Assert
        var providers = await _context.Providers
                                      .Include(p => p.Models)
                                      .ToListAsync();

        providers.Should().HaveCount(2);

        // Check OpenAI provider
        var openAi = providers.FirstOrDefault(p => p.Name == "OpenAI");
        openAi.Should().NotBeNull();
        openAi!.BaseUrl.Should().Be("https://api.openai.com/v1");
        openAi.Type.Should().Be("openai");
        openAi.IsActive.Should().BeTrue();
        openAi.Priority.Should().Be(100);
        openAi.RateLimit.RequestsPerDay.Should().Be(500);
        openAi.RateLimit.TokensPerDay.Should().Be(100000);
        // Models are no longer seeded from config - they are fetched dynamically
        openAi.Models.Should().BeEmpty();

        // Check Anthropic provider
        var anthropic = providers.FirstOrDefault(p => p.Name == "Anthropic");
        anthropic.Should().NotBeNull();
        anthropic!.BaseUrl.Should().Be("https://api.anthropic.com/v1");
        anthropic.Type.Should().Be("anthropic");
        anthropic.Headers.Should().Contain("anthropic-version");
        anthropic.IsActive.Should().BeTrue();
        anthropic.Priority.Should().Be(90);
        anthropic.RateLimit.RequestsPerDay.Should().Be(200);
        anthropic.RateLimit.TokensPerDay.Should().Be(50000);
        anthropic.Models.Should().BeEmpty();
    }

    [Fact]
    public async Task InitializeAsync_DoesNotSeed_WhenProvidersAlreadyExist()
    {
        // Arrange
        var existingProvider = new Provider
        {
            Name = "Existing Provider",
            BaseUrl = "https://api.existing.com/v1",
            ApiKey = "existing-key",
            IsActive = true,
            RateLimitJson = "{\"RequestsPerDay\": 100, \"TokensPerDay\": 50000}"
        };

        _context.Providers.Add(existingProvider);
        await _context.SaveChangesAsync();

        // Act
        await SeedData.InitializeAsync(_context, _appOptions);

        // Assert
        var providers = await _context.Providers.ToListAsync();
        providers.Should().HaveCount(1);
        providers[0].Name.Should().Be("Existing Provider");
    }

    [Fact]
    public async Task InitializeAsync_ResolvesApiKeysFromEnvironmentVariables()
    {
        // Arrange
        Environment.SetEnvironmentVariable("OPENAI_API_KEY", "env-openai-key");
        Environment.SetEnvironmentVariable("ANTHROPIC_API_KEY", "env-anthropic-key");

try
            {
                // Act
                await SeedData.InitializeAsync(_context, _appOptions);

                // Assert
            var openAi = await _context.Providers.FirstOrDefaultAsync(p => p.Name == "OpenAI");
            openAi.Should().NotBeNull();
            openAi!.ApiKey.Should().Be("env-openai-key");

            var anthropic = await _context.Providers.FirstOrDefaultAsync(p => p.Name == "Anthropic");
            anthropic.Should().NotBeNull();
            anthropic!.ApiKey.Should().Be("env-anthropic-key");
        }
        finally
        {
            Environment.SetEnvironmentVariable("OPENAI_API_KEY", null);
            Environment.SetEnvironmentVariable("ANTHROPIC_API_KEY", null);
        }
    }

    [Fact]
    public async Task InitializeAsync_UsesFallback_WhenNoConfigProvided()
    {
        // Act
        await SeedData.InitializeAsync(_context, Options.Create<AppOptions>(null!));

        // Assert - should use minimal fallback
        var providers = await _context.Providers
                                      .Include(p => p.Models)
                                      .ToListAsync();

        providers.Should().HaveCount(1);
        var openAi = providers.FirstOrDefault(p => p.Name == "OpenAI");
        openAi.Should().NotBeNull();
        openAi!.RateLimit.RequestsPerDay.Should().Be(500);
        openAi.RateLimit.TokensPerDay.Should().Be(100000);
        // Models are no longer seeded from config
        openAi.Models.Should().BeEmpty();
    }
}