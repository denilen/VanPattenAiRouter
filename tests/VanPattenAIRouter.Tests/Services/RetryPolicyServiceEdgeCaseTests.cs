using System.Reflection;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using VanPattenAIRouter.Models;
using VanPattenAIRouter.Services;
using Xunit;

namespace VanPattenAIRouter.Tests.Services;

public class RetryPolicyServiceEdgeCaseTests
{
    private readonly Mock<ILogger<RetryPolicyService>> _mockLogger;
    private readonly RetryPolicyService _service;

    public RetryPolicyServiceEdgeCaseTests()
    {
        _mockLogger = new Mock<ILogger<RetryPolicyService>>();
        var config = new RetryConfig
        {
            MaxAttempts = 3,
            BaseDelaySeconds = 2,
            MaxDelaySeconds = 30,
            ExponentialBackoff = true,
            RetryableStatusCodes = new List<int> { 408, 429, 500, 502, 503, 504 },
            RetryableExceptions = new List<string>
                { "HttpRequestException", "TaskCanceledException", "TimeoutException" }
        };
        var options = new Mock<IOptions<RetryConfig>>();
        options.Setup(o => o.Value).Returns(config);
        _service = new RetryPolicyService(options.Object, _mockLogger.Object);
    }

    [Fact]
    public void GetRetryPolicy_WithCustomConfig_UsesCustomValues()
    {
        // Arrange
        var config = new RetryConfig
        {
            MaxAttempts = 5,
            BaseDelaySeconds = 1,
            MaxDelaySeconds = 10,
            ExponentialBackoff = false
        };
        var options = new Mock<IOptions<RetryConfig>>();
        options.Setup(o => o.Value).Returns(config);
        var service = new RetryPolicyService(options.Object, _mockLogger.Object);

        // Act
        var policy = service.GetRetryPolicy<string>();

        // Assert
        policy.Should().NotBeNull();
    }

    [Fact]
    public void IsRetryableException_WithWrappedHttpRequestException_ReturnsFalse()
    {
        // Arrange - Inner exception is HttpRequestException but outer is not
        var exception = new InvalidOperationException("Wrapper", new HttpRequestException("429 Too Many Requests"));

        // Act
        var result = _service.IsRetryableException(exception);

        // Assert - InvalidOperationException is not in retryable list, so false
        result.Should().BeFalse();
    }

    [Fact]
    public void IsRetryableException_HttpRequestException_WithNonRetryableStatusCodeInMessage_ReturnsFalse()
    {
        // Arrange - HttpRequestException with 404 in message
        var exception = new HttpRequestException("Not found: 404");

        // Act
        var result = _service.IsRetryableException(exception);

        // Assert - 404 is not retryable
        result.Should().BeFalse();
    }

    [Fact]
    public void IsRetryableException_HttpRequestException_WithMultipleCodesInMessage_ReturnsTrueIfAnyRetryable()
    {
        // Arrange - Both 404 and 429 in message
        var exception = new HttpRequestException("Error 404 then 429");

        // Act
        var result = _service.IsRetryableException(exception);

        // Assert - 429 is retryable
        result.Should().BeTrue();
    }

    [Theory]
    [InlineData(408)]
    [InlineData(429)]
    [InlineData(500)]
    [InlineData(502)]
    [InlineData(503)]
    [InlineData(504)]
    public void IsRetryableStatusCode_AllRetryableCodes_ReturnsTrue(int code)
    {
        // Arrange
        var method = typeof(RetryPolicyService).GetMethod("IsRetryableStatusCode",
                                                          BindingFlags.NonPublic | BindingFlags.Instance);
        var exception = new HttpRequestException($"Status code {code}");

        // Act
        var result = (bool)method!.Invoke(_service, new object[] { exception })!;

        // Assert
        result.Should().BeTrue();
    }

    [Theory]
    [InlineData(400)]
    [InlineData(401)]
    [InlineData(403)]
    [InlineData(404)]
    [InlineData(422)]
    public void IsRetryableStatusCode_NonRetryableCodes_ReturnsFalse(int code)
    {
        // Arrange
        var method = typeof(RetryPolicyService).GetMethod("IsRetryableStatusCode",
                                                          BindingFlags.NonPublic | BindingFlags.Instance);
        var exception = new HttpRequestException($"Status code {code}");

        // Act
        var result = (bool)method!.Invoke(_service, new object[] { exception })!;

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void GetDelay_WithMaxAttemptsLessThanRetryAttempt_ReturnsMaxDelay()
    {
        // Arrange
        var config = new RetryConfig
        {
            BaseDelaySeconds = 2,
            MaxDelaySeconds = 5,
            ExponentialBackoff = true
        };
        var options = new Mock<IOptions<RetryConfig>>();
        options.Setup(o => o.Value).Returns(config);
        var service = new RetryPolicyService(options.Object, _mockLogger.Object);

        // Act - retryAttempt = 10, max attempts effectively = 5 (but we test delay capping)
        var method = typeof(RetryPolicyService).GetMethod("GetDelay",
                                                          BindingFlags.NonPublic | BindingFlags.Instance);
        var result = (TimeSpan)method!.Invoke(service, new object[] { 10 })!;

        // Assert - Should be capped at MaxDelaySeconds
        result.Should().Be(TimeSpan.FromSeconds(5));
    }
}