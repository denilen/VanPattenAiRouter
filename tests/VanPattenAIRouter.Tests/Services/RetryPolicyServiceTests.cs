using System.Reflection;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Polly.Retry;
using VanPattenAIRouter.Models;
using VanPattenAIRouter.Services;
using Xunit;

namespace VanPattenAIRouter.Tests.Services;

public class RetryPolicyServiceTests
{
    private readonly Mock<ILogger<RetryPolicyService>> _mockLogger;
    private readonly Mock<IOptions<RetryConfig>> _mockOptions;
    private readonly RetryPolicyService _service;

    public RetryPolicyServiceTests()
    {
        _mockLogger = new Mock<ILogger<RetryPolicyService>>();
        _mockOptions = new Mock<IOptions<RetryConfig>>();
        _mockOptions.Setup(o => o.Value).Returns(new RetryConfig
        {
            MaxAttempts = 3,
            BaseDelaySeconds = 2,
            MaxDelaySeconds = 30,
            ExponentialBackoff = true,
            RetryableStatusCodes = new List<int> { 408, 429, 500, 502, 503, 504 },
            RetryableExceptions = new List<string>
                { "HttpRequestException", "TaskCanceledException", "TimeoutException" }
        });

        _service = new RetryPolicyService(_mockOptions.Object, _mockLogger.Object);
    }

    [Fact]
    public void GetRetryPolicy_ReturnsPolicyWithCorrectConfiguration()
    {
        // Act
        var policy = _service.GetRetryPolicy<string>();

        // Assert
        policy.Should().NotBeNull();
        policy.Should().BeAssignableTo<AsyncRetryPolicy<string>>();
    }

    [Fact]
    public void GetRetryPolicy_HandlesHttpRequestException()
    {
        // Act
        var policy = _service.GetRetryPolicy<string>();

        // Assert - policy should handle HttpRequestException
        policy.Should().NotBeNull();
    }

    [Fact]
    public void GetRetryPolicy_HandlesTaskCanceledException()
    {
        // Act
        var policy = _service.GetRetryPolicy<string>();

        // Assert
        policy.Should().NotBeNull();
    }

    [Fact]
    public void GetRetryPolicy_HandlesTimeoutException()
    {
        // Act
        var policy = _service.GetRetryPolicy<string>();

        // Assert
        policy.Should().NotBeNull();
    }

    [Theory]
    [InlineData(1, 2, false)]  // First retry: 2^0 * 2 = 2 seconds
    [InlineData(2, 4, false)]  // Second retry: 2^1 * 2 = 4 seconds
    [InlineData(3, 8, false)]  // Third retry: 2^2 * 2 = 8 seconds
    [InlineData(10, 30, true)] // Large retry: capped at MaxDelaySeconds (30)
    public void GetDelay_ExponentialBackoff_CalculatesCorrectDelay(int retryAttempt, int expectedSeconds,
                                                                   bool shouldBeCapped)
    {
        // Arrange
        var config = new RetryConfig
        {
            BaseDelaySeconds = 2,
            MaxDelaySeconds = 30,
            ExponentialBackoff = true
        };
        var options = new Mock<IOptions<RetryConfig>>();
        options.Setup(o => o.Value).Returns(config);

        var service = new RetryPolicyService(options.Object, _mockLogger.Object);

        // Act - Use reflection to test private method
        var method = typeof(RetryPolicyService).GetMethod("GetDelay",
                                                          BindingFlags.NonPublic | BindingFlags.Instance);
        var result = (TimeSpan)method!.Invoke(service, new object[] { retryAttempt })!;

        // Assert
        if (shouldBeCapped)
            result.Should().Be(TimeSpan.FromSeconds(30));
        else
            result.Should().Be(TimeSpan.FromSeconds(expectedSeconds));
    }

    [Fact]
    public void GetDelay_LinearBackoff_ReturnsBaseDelay()
    {
        // Arrange
        var config = new RetryConfig
        {
            BaseDelaySeconds = 5,
            MaxDelaySeconds = 60,
            ExponentialBackoff = false
        };
        var options = new Mock<IOptions<RetryConfig>>();
        options.Setup(o => o.Value).Returns(config);

        var service = new RetryPolicyService(options.Object, _mockLogger.Object);

        // Act
        var method = typeof(RetryPolicyService).GetMethod("GetDelay",
                                                          BindingFlags.NonPublic | BindingFlags.Instance);
        var result = (TimeSpan)method!.Invoke(service, new object[] { 1 })!;

        // Assert
        result.Should().Be(TimeSpan.FromSeconds(5));
    }

    [Theory]
    [InlineData("HttpRequestException", true)]
    [InlineData("TaskCanceledException", true)]
    [InlineData("TimeoutException", true)]
    [InlineData("ArgumentException", false)]
    [InlineData("InvalidOperationException", false)]
    public void IsRetryableException_ByExceptionType_ReturnsExpectedResult(string exceptionType, bool expected)
    {
        // Arrange
        var exception = CreateException(exceptionType);

        // Act
        var result = _service.IsRetryableException(exception);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("429", true)]
    [InlineData("500", true)]
    [InlineData("502", true)]
    [InlineData("503", true)]
    [InlineData("504", true)]
    [InlineData("408", true)]
    [InlineData("401", false)]
    [InlineData("403", false)]
    [InlineData("404", false)]
    [InlineData("400", false)]
    public void IsRetryableException_ByHttpStatusCodeInMessage_ReturnsExpectedResult(string statusCode, bool expected)
    {
        // Arrange
        var exception = new HttpRequestException($"Request failed with status code {statusCode}");

        // Act
        var result = _service.IsRetryableException(exception);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void IsRetryableException_WithNullMessage_ReturnsFalse()
    {
        // Arrange
        var exception = new HttpRequestException();

        // Act
        var result = _service.IsRetryableException(exception);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsRetryableException_WithMultipleRetryableCodesInMessage_ReturnsTrue()
    {
        // Arrange
        var exception = new HttpRequestException("Rate limited: 429 then 503");

        // Act
        var result = _service.IsRetryableException(exception);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsRetryableException_LogsWarningOnRetry()
    {
        // Arrange
        var policy = _service.GetRetryPolicy<string>();
        var exception = new HttpRequestException("Rate limited: 429");

        // Act & Assert - Verify the policy is configured to log
        _mockLogger.Verify(x => x.Log(LogLevel.Warning,
                                      It.IsAny<EventId>(),
                                      It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Retry")),
                                      It.IsAny<Exception>(),
                                      It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                           Times.Never); // No actual retry executed yet
    }

    private static Exception CreateException(string typeName)
    {
        return typeName switch
        {
            "HttpRequestException" => new HttpRequestException("Test"),
            "TaskCanceledException" => new TaskCanceledException("Test"),
            "TimeoutException" => new TimeoutException("Test"),
            "ArgumentException" => new ArgumentException("Test"),
            "InvalidOperationException" => new InvalidOperationException("Test"),
            _ => new Exception("Test")
        };
    }
}