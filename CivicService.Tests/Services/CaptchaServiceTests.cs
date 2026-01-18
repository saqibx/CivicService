using CivicService.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using System.Net;

namespace CivicService.Tests.Services;

public class CaptchaServiceTests
{
    private readonly Mock<ILogger<CaptchaService>> _loggerMock;

    public CaptchaServiceTests()
    {
        _loggerMock = new Mock<ILogger<CaptchaService>>();
    }

    private static IConfiguration CreateConfig(string? secretKey = null)
    {
        var config = new Dictionary<string, string?>
        {
            ["ReCaptcha:SecretKey"] = secretKey,
            ["ReCaptcha:MinScore"] = "0.5"
        };
        return new ConfigurationBuilder()
            .AddInMemoryCollection(config)
            .Build();
    }

    [Fact]
    public void IsConfigured_WithSecretKey_ReturnsTrue()
    {
        // Arrange
        var config = CreateConfig("test-secret-key");
        var httpClient = new HttpClient();
        var service = new CaptchaService(httpClient, config, _loggerMock.Object);

        // Assert
        Assert.True(service.IsConfigured);
    }

    [Fact]
    public void IsConfigured_WithoutSecretKey_ReturnsFalse()
    {
        // Arrange
        var config = CreateConfig(null);
        var httpClient = new HttpClient();
        var service = new CaptchaService(httpClient, config, _loggerMock.Object);

        // Assert
        Assert.False(service.IsConfigured);
    }

    [Fact]
    public async Task VerifyAsync_WhenNotConfigured_ReturnsTrue()
    {
        // Arrange
        var config = CreateConfig(null);
        var httpClient = new HttpClient();
        var service = new CaptchaService(httpClient, config, _loggerMock.Object);

        // Act
        var result = await service.VerifyAsync("any-token", "submit_request");

        // Assert
        Assert.True(result); // Should pass when not configured (dev mode)
    }

    [Fact]
    public async Task VerifyAsync_WithEmptyToken_ReturnsFalse()
    {
        // Arrange
        var config = CreateConfig("test-secret-key");
        var httpClient = new HttpClient();
        var service = new CaptchaService(httpClient, config, _loggerMock.Object);

        // Act
        var result = await service.VerifyAsync("", "submit_request");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task VerifyAsync_WithNullToken_ReturnsFalse()
    {
        // Arrange
        var config = CreateConfig("test-secret-key");
        var httpClient = new HttpClient();
        var service = new CaptchaService(httpClient, config, _loggerMock.Object);

        // Act
        var result = await service.VerifyAsync(null!, "submit_request");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task VerifyAsync_WithValidResponse_ReturnsTrue()
    {
        // Arrange
        var config = CreateConfig("test-secret-key");
        var mockHandler = new Mock<HttpMessageHandler>();
        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent("""
                    {
                        "success": true,
                        "score": 0.9,
                        "action": "submit_request",
                        "challenge_ts": "2024-01-01T00:00:00Z",
                        "hostname": "localhost"
                    }
                    """)
            });

        var httpClient = new HttpClient(mockHandler.Object);
        var service = new CaptchaService(httpClient, config, _loggerMock.Object);

        // Act
        var result = await service.VerifyAsync("valid-token", "submit_request");

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task VerifyAsync_WithLowScore_ReturnsFalse()
    {
        // Arrange
        var config = CreateConfig("test-secret-key");
        var mockHandler = new Mock<HttpMessageHandler>();
        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent("""
                    {
                        "success": true,
                        "score": 0.2,
                        "action": "submit_request"
                    }
                    """)
            });

        var httpClient = new HttpClient(mockHandler.Object);
        var service = new CaptchaService(httpClient, config, _loggerMock.Object);

        // Act
        var result = await service.VerifyAsync("suspicious-token", "submit_request");

        // Assert
        Assert.False(result); // Score below 0.5 threshold
    }

    [Fact]
    public async Task VerifyAsync_WithActionMismatch_ReturnsFalse()
    {
        // Arrange
        var config = CreateConfig("test-secret-key");
        var mockHandler = new Mock<HttpMessageHandler>();
        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent("""
                    {
                        "success": true,
                        "score": 0.9,
                        "action": "wrong_action"
                    }
                    """)
            });

        var httpClient = new HttpClient(mockHandler.Object);
        var service = new CaptchaService(httpClient, config, _loggerMock.Object);

        // Act
        var result = await service.VerifyAsync("token", "submit_request");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task VerifyAsync_WithFailedVerification_ReturnsFalse()
    {
        // Arrange
        var config = CreateConfig("test-secret-key");
        var mockHandler = new Mock<HttpMessageHandler>();
        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent("""
                    {
                        "success": false,
                        "error-codes": ["invalid-input-response"]
                    }
                    """)
            });

        var httpClient = new HttpClient(mockHandler.Object);
        var service = new CaptchaService(httpClient, config, _loggerMock.Object);

        // Act
        var result = await service.VerifyAsync("invalid-token", "submit_request");

        // Assert
        Assert.False(result);
    }
}
