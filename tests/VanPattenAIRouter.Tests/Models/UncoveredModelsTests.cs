using FluentAssertions;
using VanPattenAIRouter.Models;
using Xunit;

namespace VanPattenAIRouter.Tests.Models;

public class ModelGroupTests
{
    [Fact]
    public void ModelGroup_DefaultValues_AreCorrect()
    {
        // Act
        var group = new ModelGroup();

        // Assert
        group.Name.Should().Be("");
        group.Type.Should().Be("");
        group.Provider.Should().Be("");
        group.Models.Should().NotBeNull();
        group.Models.Should().BeEmpty();
    }

    [Fact]
    public void ModelGroup_CanBePopulated()
    {
        // Arrange
        var models = new List<ModelInfo>
        {
            new() { Id = "model1", Object = "model", Created = 123456, OwnedBy = "Provider1" },
            new() { Id = "model2", Object = "model", Created = 123457, OwnedBy = "Provider1" }
        };

        // Act
        var group = new ModelGroup
        {
            Name = "Test Group",
            Type = "free",
            Provider = "TestProvider",
            Models = models
        };

        // Assert
        group.Name.Should().Be("Test Group");
        group.Type.Should().Be("free");
        group.Provider.Should().Be("TestProvider");
        group.Models.Should().HaveCount(2);
        group.Models[0].Id.Should().Be("model1");
        group.Models[1].Id.Should().Be("model2");
    }
}

public class ModelsGroupedResponseTests
{
    [Fact]
    public void ModelsGroupedResponse_DefaultValues_AreCorrect()
    {
        // Act
        var response = new ModelsGroupedResponse();

        // Assert
        response.Object.Should().Be("list");
        response.Groups.Should().NotBeNull();
        response.Groups.Should().BeEmpty();
    }

    [Fact]
    public void ModelsGroupedResponse_CanBePopulated()
    {
        // Arrange
        var groups = new List<ModelGroup>
        {
            new()
            {
                Name = "Free Models",
                Type = "free",
                Provider = "Multiple",
                Models = new List<ModelInfo>
                {
                    new() { Id = "free-model-1", Object = "model", Created = 123, OwnedBy = "Provider1" }
                }
            },
            new()
            {
                Name = "OpenAI",
                Type = "provider",
                Provider = "OpenAI",
                Models = new List<ModelInfo>
                {
                    new() { Id = "gpt-4", Object = "model", Created = 456, OwnedBy = "OpenAI" }
                }
            }
        };

        // Act
        var response = new ModelsGroupedResponse
        {
            Object = "list",
            Groups = groups
        };

        // Assert
        response.Object.Should().Be("list");
        response.Groups.Should().HaveCount(2);
        response.Groups[0].Name.Should().Be("Free Models");
        response.Groups[0].Models.Should().HaveCount(1);
        response.Groups[1].Name.Should().Be("OpenAI");
        response.Groups[1].Models.Should().HaveCount(1);
    }
}

public class ProviderConfigTests
{
    [Fact]
    public void ProviderConfig_DefaultValues_AreCorrect()
    {
        // Act
        var config = new ProviderConfig();

        // Assert
        config.Name.Should().Be("");
        config.BaseUrl.Should().Be("");
        config.ApiKey.Should().Be("");
        config.Type.Should().Be("openai");
        config.IsActive.Should().BeTrue();
        config.Priority.Should().Be(0);
        config.IsVirtual.Should().BeFalse();
        config.RateLimit.Should().NotBeNull();
        config.Models.Should().NotBeNull();
        config.Models.Should().BeEmpty();
    }

    [Fact]
    public void ProviderConfig_CanBeFullyPopulated()
    {
        // Arrange
        var rateLimit = new RateLimitConfig
        {
            RequestsPerDay = 500,
            TokensPerDay = 50000,
            RequestsPerHour = 100,
            TokensPerHour = 5000
        };

        var models = new List<ProviderModelConfig>
        {
            new() { Name = "gpt-4", Description = "GPT-4", Mode = "normal", IsEnabled = true },
            new() { Name = "gpt-3.5", Description = "GPT-3.5", Mode = "normal", IsEnabled = true }
        };

        // Act
        var config = new ProviderConfig
        {
            Name = "Test Provider",
            BaseUrl = "https://api.test.com/v1",
            ApiKey = "test-key",
            Type = "openai",
            IsActive = true,
            Priority = 50,
            IsVirtual = false,
            Headers = new Dictionary<string, string> { { "Custom", "Header" } },
            RateLimit = rateLimit,
            Models = models
        };

        // Assert
        config.Name.Should().Be("Test Provider");
        config.BaseUrl.Should().Be("https://api.test.com/v1");
        config.ApiKey.Should().Be("test-key");
        config.Type.Should().Be("openai");
        config.IsActive.Should().BeTrue();
        config.Priority.Should().Be(50);
        config.IsVirtual.Should().BeFalse();
        config.Headers.Should().ContainKey("Custom");
        config.RateLimit.RequestsPerDay.Should().Be(500);
        config.RateLimit.TokensPerDay.Should().Be(50000);
        config.RateLimit.RequestsPerHour.Should().Be(100);
        config.RateLimit.TokensPerHour.Should().Be(5000);
        config.Models.Should().HaveCount(2);
    }
}

public class ProviderModelConfigTests
{
    [Fact]
    public void ProviderModelConfig_DefaultValues_AreCorrect()
    {
        // Act
        var config = new ProviderModelConfig();

        // Assert
        config.Name.Should().Be("");
        config.Description.Should().Be("");
        config.Mode.Should().Be("normal");
        config.IsEnabled.Should().BeTrue();
    }

    [Fact]
    public void ProviderModelConfig_CanBePopulated()
    {
        // Act
        var config = new ProviderModelConfig
        {
            Name = "custom-model",
            Description = "Custom model description",
            Mode = "auto-free",
            IsEnabled = false
        };

        // Assert
        config.Name.Should().Be("custom-model");
        config.Description.Should().Be("Custom model description");
        config.Mode.Should().Be("auto-free");
        config.IsEnabled.Should().BeFalse();
    }
}

public class RetryConfigTests
{
    [Fact]
    public void RetryConfig_DefaultValues_AreCorrect()
    {
        // Act
        var config = new RetryConfig();

        // Assert
        config.MaxAttempts.Should().Be(3);
        config.BaseDelaySeconds.Should().Be(2);
        config.MaxDelaySeconds.Should().Be(30);
        config.ExponentialBackoff.Should().BeTrue();
        config.RetryableStatusCodes.Should().NotBeNull();
        config.RetryableStatusCodes.Should().BeEquivalentTo(new[] { 408, 429, 500, 502, 503, 504 });
        config.RetryableExceptions.Should().NotBeNull();
        config.RetryableExceptions.Should()
              .BeEquivalentTo("HttpRequestException", "TaskCanceledException", "TimeoutException");
    }

    [Fact]
    public void RetryConfig_CanBeCustomized()
    {
        // Act
        var config = new RetryConfig
        {
            MaxAttempts = 5,
            BaseDelaySeconds = 5,
            MaxDelaySeconds = 60,
            ExponentialBackoff = false,
            RetryableStatusCodes = new List<int> { 429, 500, 503 },
            RetryableExceptions = new List<string> { "HttpRequestException" }
        };

        // Assert
        config.MaxAttempts.Should().Be(5);
        config.BaseDelaySeconds.Should().Be(5);
        config.MaxDelaySeconds.Should().Be(60);
        config.ExponentialBackoff.Should().BeFalse();
        config.RetryableStatusCodes.Should().BeEquivalentTo(new[] { 429, 500, 503 });
        config.RetryableExceptions.Should().BeEquivalentTo("HttpRequestException");
    }
}