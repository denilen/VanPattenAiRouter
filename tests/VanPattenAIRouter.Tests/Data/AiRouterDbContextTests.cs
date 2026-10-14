using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using VanPattenAIRouter.Data;
using Xunit;

namespace VanPattenAIRouter.Tests.Data;

public class AiRouterDbContextTests : IDisposable
{
    private readonly AiRouterDbContext _context;

    public AiRouterDbContextTests()
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

    [Fact]
    public void DbContext_CanAddAndRetrieveProvider()
    {
        // Arrange
        var provider = new Provider
        {
            Name = "Test Provider",
            BaseUrl = "https://api.test.com/v1",
            ApiKey = "test-key",
            Type = "openai",
            Headers = "{\"Authorization\": \"Bearer token\"}",
            IsActive = true,
            Priority = 50
        };

        // Act
        _context.Providers.Add(provider);
        _context.SaveChanges();

        // Assert
        var retrieved = _context.Providers.FirstOrDefault(p => p.Name == "Test Provider");
        retrieved.Should().NotBeNull();
        retrieved!.Name.Should().Be("Test Provider");
        retrieved.BaseUrl.Should().Be("https://api.test.com/v1");
        retrieved.ApiKey.Should().Be("test-key");
        retrieved.Type.Should().Be("openai");
        retrieved.Headers.Should().Be("{\"Authorization\": \"Bearer token\"}");
        retrieved.IsActive.Should().BeTrue();
        retrieved.Priority.Should().Be(50);
        retrieved.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        retrieved.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void DbContext_CanAddAndRetrieveProviderWithModels()
    {
        // Arrange
        var provider = new Provider
        {
            Name = "Provider with Models",
            BaseUrl = "https://api.test.com/v1",
            ApiKey = "test-key",
            IsActive = true,
            Models = new List<ProviderModel>
            {
                new() { ModelName = "gpt-4", ProviderModelName = "gpt-4-turbo", IsEnabled = true },
                new() { ModelName = "gpt-3.5", ProviderModelName = "gpt-3.5-turbo", IsEnabled = false }
            }
        };

        // Act
        _context.Providers.Add(provider);
        _context.SaveChanges();

        // Assert
        var retrieved = _context.Providers
                                .Include(p => p.Models)
                                .FirstOrDefault(p => p.Name == "Provider with Models");

        retrieved.Should().NotBeNull();
        retrieved!.Models.Should().HaveCount(2);
        retrieved.Models[0].ModelName.Should().Be("gpt-4");
        retrieved.Models[0].ProviderModelName.Should().Be("gpt-4-turbo");
        retrieved.Models[0].IsEnabled.Should().BeTrue();
        retrieved.Models[1].ModelName.Should().Be("gpt-3.5");
        retrieved.Models[1].ProviderModelName.Should().Be("gpt-3.5-turbo");
        retrieved.Models[1].IsEnabled.Should().BeFalse();
    }

    [Fact]
    public void DbContext_EnforcesUniqueProviderName()
    {
        // In-memory database doesn't enforce unique constraints
        // This test verifies the model configuration has the unique index
        var model = _context.Model.FindEntityType(typeof(Provider));
        var index = model!.GetIndexes().FirstOrDefault(i => i.Properties.Any(p => p.Name == "Name"));

        index.Should().NotBeNull();
        index!.IsUnique.Should().BeTrue();
    }

    [Fact]
    public void DbContext_EnforcesUniqueProviderModelCombination()
    {
        // In-memory database doesn't enforce unique constraints
        // This test verifies the model configuration has the unique index
        var model = _context.Model.FindEntityType(typeof(ProviderModel));
        var index = model!.GetIndexes().FirstOrDefault(i =>
                                                           i.Properties.Any(p => p.Name == "ProviderId") &&
                                                           i.Properties.Any(p => p.Name == "ModelName"));

        index.Should().NotBeNull();
        index!.IsUnique.Should().BeTrue();
    }

    [Fact]
    public void DbContext_CascadeDeletesModelsWhenProviderDeleted()
    {
        // Arrange
        var provider = new Provider
        {
            Name = "To Delete",
            BaseUrl = "https://api.test.com/v1",
            ApiKey = "test-key",
            IsActive = true,
            Models = new List<ProviderModel>
            {
                new() { ModelName = "model1", IsEnabled = true },
                new() { ModelName = "model2", IsEnabled = true }
            }
        };

        _context.Providers.Add(provider);
        _context.SaveChanges();

        var providerId = provider.Id;

        // Act
        _context.Providers.Remove(provider);
        _context.SaveChanges();

        // Assert
        _context.Providers.Find(providerId).Should().BeNull();
        _context.ProviderModels.Where(m => m.ProviderId == providerId).Should().BeEmpty();
    }

    [Fact]
    public void DbContext_CanQueryProvidersByPriority()
    {
        // Arrange
        var providers = new List<Provider>
        {
            new() { Name = "Low", BaseUrl = "https://api.low.com/v1", ApiKey = "key1", Priority = 10, IsActive = true },
            new()
            {
                Name = "Medium", BaseUrl = "https://api.medium.com/v1", ApiKey = "key2", Priority = 50, IsActive = true
            },
            new()
            {
                Name = "High", BaseUrl = "https://api.high.com/v1", ApiKey = "key3", Priority = 100, IsActive = true
            }
        };

        _context.Providers.AddRange(providers);
        _context.SaveChanges();

        // Act
        var result = _context.Providers
                             .OrderByDescending(p => p.Priority)
                             .Select(p => p.Name)
                             .ToList();

        // Assert
        result.Should().BeEquivalentTo(new[] { "High", "Medium", "Low" }, options => options.WithStrictOrdering());
    }

    [Fact]
    public void DbContext_CanQueryEnabledModelsFromActiveProviders()
    {
        // Arrange
        var activeProvider = new Provider
        {
            Name = "Active",
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
            Name = "Inactive",
            BaseUrl = "https://api.inactive.com/v1",
            ApiKey = "key2",
            Priority = 50,
            IsActive = false,
            Models = new List<ProviderModel>
            {
                new() { ModelName = "inactive-provider-model", IsEnabled = true }
            }
        };

        _context.Providers.AddRange(activeProvider, inactiveProvider);
        _context.SaveChanges();

        // Act
        var result = _context.ProviderModels
                             .Include(m => m.Provider)
                             .Where(m => m.IsEnabled && m.Provider.IsActive)
                             .OrderByDescending(m => m.Provider.Priority)
                             .Select(m => new { m.ModelName, m.Provider.Name })
                             .ToList();

        // Assert
        result.Should().HaveCount(1);
        result[0].ModelName.Should().Be("enabled-model");
        result[0].Name.Should().Be("Active");
    }

    [Fact]
    public void Provider_DefaultValues_AreCorrect()
    {
        // Act
        var provider = new Provider();

        // Assert
        provider.Id.Should().Be(0);
        provider.Name.Should().Be("");
        provider.BaseUrl.Should().Be("");
        provider.ApiKey.Should().Be("");
        provider.Type.Should().Be("openai");
        provider.Headers.Should().BeNull();
        provider.IsActive.Should().BeTrue();
        provider.Priority.Should().Be(0);
        provider.Models.Should().NotBeNull().And.BeEmpty();
    }

    [Fact]
    public void ProviderModel_DefaultValues_AreCorrect()
    {
        // Act
        var model = new ProviderModel();

        // Assert
        model.Id.Should().Be(0);
        model.ProviderId.Should().Be(0);
        model.ModelName.Should().Be("");
        model.ProviderModelName.Should().BeNull();
        model.IsEnabled.Should().BeTrue();
    }
}