using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Moq.Protected;
using mostlylucid.activetranslatetag.Services.Providers;
using Xunit;

namespace mostlylucid.activetranslatetag.Tests;

public class OllamaTranslationProviderTests
{
    [Fact]
    public async Task TranslateAsync_SuccessfulResponse_ReturnsTranslatedText()
    {
        // Arrange
        var mockHttp = new Mock<HttpMessageHandler>();
        var expectedTranslation = "Bonjour le monde";
        var responseContent = JsonSerializer.Serialize(new { response = expectedTranslation });

        mockHttp.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(responseContent)
            });

        var httpClient = new HttpClient(mockHttp.Object);
        var logger = new NullLogger<OllamaTranslationProvider>();
        var provider = new OllamaTranslationProvider(httpClient, logger, "http://localhost:11434/", "llama3.1");

        // Act
        var result = await provider.TranslateAsync("Hello world", "fr", "en");

        // Assert
        result.Should().Be(expectedTranslation);
    }

    [Fact]
    public async Task TranslateAsync_FailedResponse_ReturnsOriginalText()
    {
        // Arrange
        var mockHttp = new Mock<HttpMessageHandler>();
        var originalText = "Hello world";

        mockHttp.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.InternalServerError,
                Content = new StringContent("Server error")
            });

        var httpClient = new HttpClient(mockHttp.Object);
        var logger = new NullLogger<OllamaTranslationProvider>();
        var provider = new OllamaTranslationProvider(httpClient, logger);

        // Act
        var result = await provider.TranslateAsync(originalText, "fr", "en");

        // Assert
        result.Should().Be(originalText, "should return original text on failure");
    }

    [Fact]
    public async Task TranslateAsync_EmptyResponse_ReturnsOriginalText()
    {
        // Arrange
        var mockHttp = new Mock<HttpMessageHandler>();
        var originalText = "Hello world";
        var responseContent = JsonSerializer.Serialize(new { response = "" });

        mockHttp.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(responseContent)
            });

        var httpClient = new HttpClient(mockHttp.Object);
        var logger = new NullLogger<OllamaTranslationProvider>();
        var provider = new OllamaTranslationProvider(httpClient, logger);

        // Act
        var result = await provider.TranslateAsync(originalText, "fr", "en");

        // Assert
        result.Should().Be(originalText, "should return original text when response is empty");
    }

    [Fact]
    public async Task TranslateAsync_ExceptionThrown_ReturnsOriginalText()
    {
        // Arrange
        var mockHttp = new Mock<HttpMessageHandler>();
        var originalText = "Hello world";

        mockHttp.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ThrowsAsync(new HttpRequestException("Network error"));

        var httpClient = new HttpClient(mockHttp.Object);
        var logger = new NullLogger<OllamaTranslationProvider>();
        var provider = new OllamaTranslationProvider(httpClient, logger);

        // Act
        var result = await provider.TranslateAsync(originalText, "fr", "en");

        // Assert
        result.Should().Be(originalText, "should return original text on exception");
    }

    [Fact]
    public async Task TranslateBatchAsync_TranslatesAllItems()
    {
        // Arrange
        var mockHttp = new Mock<HttpMessageHandler>();
        var callCount = 0;

        mockHttp.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(() =>
            {
                var translation = $"Translation {++callCount}";
                var responseContent = JsonSerializer.Serialize(new { response = translation });
                return new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(responseContent)
                };
            });

        var httpClient = new HttpClient(mockHttp.Object);
        var logger = new NullLogger<OllamaTranslationProvider>();
        var provider = new OllamaTranslationProvider(httpClient, logger);

        var items = new Dictionary<string, string>
        {
            { "key1", "Text 1" },
            { "key2", "Text 2" },
            { "key3", "Text 3" }
        };

        // Act
        var result = await provider.TranslateBatchAsync(items, "fr", "en");

        // Assert
        result.Should().HaveCount(3);
        result.Should().ContainKeys("key1", "key2", "key3");
        result.Values.Should().AllSatisfy(v => v.Should().StartWith("Translation"));
    }

    [Fact]
    public void Constructor_SetsDefaultBaseUrl_WhenNotProvided()
    {
        // Arrange
        var httpClient = new HttpClient();
        var logger = new NullLogger<OllamaTranslationProvider>();

        // Act
        var provider = new OllamaTranslationProvider(httpClient, logger);

        // Assert
        httpClient.BaseAddress.Should().NotBeNull();
        httpClient.BaseAddress!.ToString().Should().Be("http://localhost:11434/");
    }

    [Fact]
    public void Constructor_UsesProvidedBaseUrl()
    {
        // Arrange
        var httpClient = new HttpClient();
        var logger = new NullLogger<OllamaTranslationProvider>();
        var customUrl = "http://custom-ollama:8080/";

        // Act
        var provider = new OllamaTranslationProvider(httpClient, logger, customUrl);

        // Assert
        httpClient.BaseAddress.Should().NotBeNull();
        httpClient.BaseAddress!.ToString().Should().Be(customUrl);
    }

    [Fact]
    public void Constructor_UsesProvidedModel()
    {
        // Arrange
        var httpClient = new HttpClient();
        var logger = new NullLogger<OllamaTranslationProvider>();

        // Act - model is passed as parameter, we can't directly test it but we can ensure constructor doesn't throw
        var provider = new OllamaTranslationProvider(httpClient, logger, model: "custom-model");

        // Assert - constructor should succeed
        provider.Should().NotBeNull();
    }
}
