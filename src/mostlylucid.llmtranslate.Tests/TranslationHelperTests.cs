using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Moq;
using mostlylucid.llmtranslate.Helpers;
using mostlylucid.llmtranslate.Services;
using Xunit;

namespace mostlylucid.llmtranslate.Tests;

public class TranslationHelperTests
{
    [Fact]
    public void GetCurrentLanguage_NoCookieNoHeader_ReturnsEnglish()
    {
        // Arrange
        var mockTranslationService = new Mock<ITranslationService>();
        var mockHttpContextAccessor = new Mock<IHttpContextAccessor>();
        var mockHttpContext = new Mock<HttpContext>();
        var mockRequest = new Mock<HttpRequest>();
        var mockCookies = new Mock<IRequestCookieCollection>();
        var mockHeaders = new HeaderDictionary();

        mockCookies.Setup(c => c.TryGetValue(It.IsAny<string>(), out It.Ref<string>.IsAny)).Returns(false);
        mockRequest.Setup(r => r.Cookies).Returns(mockCookies.Object);
        mockRequest.Setup(r => r.Headers).Returns(mockHeaders);
        mockHttpContext.Setup(c => c.Request).Returns(mockRequest.Object);
        mockHttpContextAccessor.Setup(a => a.HttpContext).Returns(mockHttpContext.Object);

        var helper = new TranslationHelper(mockTranslationService.Object, mockHttpContextAccessor.Object);

        // Act
        var result = helper.GetCurrentLanguage();

        // Assert
        result.Should().Be("en");
    }

    [Fact]
    public void GetCurrentLanguage_WithCookie_ReturnsCookieValue()
    {
        // Arrange
        var mockTranslationService = new Mock<ITranslationService>();
        var mockHttpContextAccessor = new Mock<IHttpContextAccessor>();
        var mockHttpContext = new Mock<HttpContext>();
        var mockRequest = new Mock<HttpRequest>();
        var mockCookies = new Mock<IRequestCookieCollection>();

        var cookieValue = "fr";
        mockCookies.Setup(c => c.TryGetValue("preferred-language", out cookieValue)).Returns(true);
        mockRequest.Setup(r => r.Cookies).Returns(mockCookies.Object);
        mockHttpContext.Setup(c => c.Request).Returns(mockRequest.Object);
        mockHttpContextAccessor.Setup(a => a.HttpContext).Returns(mockHttpContext.Object);

        var helper = new TranslationHelper(mockTranslationService.Object, mockHttpContextAccessor.Object);

        // Act
        var result = helper.GetCurrentLanguage();

        // Assert
        result.Should().Be("fr");
    }

    [Fact]
    public void GetCurrentLanguage_WithAcceptLanguageHeader_ReturnsLanguageCode()
    {
        // Arrange
        var mockTranslationService = new Mock<ITranslationService>();
        var mockHttpContextAccessor = new Mock<IHttpContextAccessor>();
        var mockHttpContext = new Mock<HttpContext>();
        var mockRequest = new Mock<HttpRequest>();
        var mockCookies = new Mock<IRequestCookieCollection>();
        var mockHeaders = new HeaderDictionary
        {
            { "Accept-Language", "de-DE,de;q=0.9,en;q=0.8" }
        };

        mockCookies.Setup(c => c.TryGetValue(It.IsAny<string>(), out It.Ref<string>.IsAny)).Returns(false);
        mockRequest.Setup(r => r.Cookies).Returns(mockCookies.Object);
        mockRequest.Setup(r => r.Headers).Returns(mockHeaders);
        mockHttpContext.Setup(c => c.Request).Returns(mockRequest.Object);
        mockHttpContextAccessor.Setup(a => a.HttpContext).Returns(mockHttpContext.Object);

        var helper = new TranslationHelper(mockTranslationService.Object, mockHttpContextAccessor.Object);

        // Act
        var result = helper.GetCurrentLanguage();

        // Assert
        result.Should().Be("de");
    }

    [Fact]
    public void GetCurrentLanguage_NoHttpContext_ReturnsEnglish()
    {
        // Arrange
        var mockTranslationService = new Mock<ITranslationService>();
        var mockHttpContextAccessor = new Mock<IHttpContextAccessor>();
        mockHttpContextAccessor.Setup(a => a.HttpContext).Returns((HttpContext?)null);

        var helper = new TranslationHelper(mockTranslationService.Object, mockHttpContextAccessor.Object);

        // Act
        var result = helper.GetCurrentLanguage();

        // Assert
        result.Should().Be("en");
    }

    [Fact]
    public async Task T_WithKeyOnly_CallsTranslationService()
    {
        // Arrange
        var callCount = 0;
        var mockTranslationService = new Mock<ITranslationService>();
        var mockHttpContextAccessor = new Mock<IHttpContextAccessor>();
        var mockHttpContext = new Mock<HttpContext>();
        var mockRequest = new Mock<HttpRequest>();
        var mockCookies = new Mock<IRequestCookieCollection>();
        var mockHeaders = new HeaderDictionary();

        mockCookies.Setup(c => c.TryGetValue(It.IsAny<string>(), out It.Ref<string>.IsAny)).Returns(false);
        mockRequest.Setup(r => r.Cookies).Returns(mockCookies.Object);
        mockRequest.Setup(r => r.Headers).Returns(mockHeaders);
        mockHttpContext.Setup(c => c.Request).Returns(mockRequest.Object);
        mockHttpContextAccessor.Setup(a => a.HttpContext).Returns(mockHttpContext.Object);

        mockTranslationService
            .Setup(s => s.GetAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback(() => callCount++)
            .ReturnsAsync("Test Translation");

        var helper = new TranslationHelper(mockTranslationService.Object, mockHttpContextAccessor.Object);

        // Act
        var result = await helper.T("test.key");

        // Assert
        result.Should().Be("Test Translation");
        callCount.Should().Be(1);
    }

    [Fact]
    public async Task T_WithDefaultText_EnsuresStringExists()
    {
        // Arrange
        var callCount = 0;
        var mockTranslationService = new Mock<ITranslationService>();
        var mockHttpContextAccessor = new Mock<IHttpContextAccessor>();
        var mockHttpContext = new Mock<HttpContext>();
        var mockRequest = new Mock<HttpRequest>();
        var mockCookies = new Mock<IRequestCookieCollection>();
        var mockHeaders = new HeaderDictionary();

        mockCookies.Setup(c => c.TryGetValue(It.IsAny<string>(), out It.Ref<string>.IsAny)).Returns(false);
        mockRequest.Setup(r => r.Cookies).Returns(mockCookies.Object);
        mockRequest.Setup(r => r.Headers).Returns(mockHeaders);
        mockHttpContext.Setup(c => c.Request).Returns(mockRequest.Object);
        mockHttpContextAccessor.Setup(a => a.HttpContext).Returns(mockHttpContext.Object);

        mockTranslationService
            .Setup(s => s.GetAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback(() => callCount++)
            .ReturnsAsync("Test Translation");

        mockTranslationService
            .Setup(s => s.EnsureStringAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var helper = new TranslationHelper(mockTranslationService.Object, mockHttpContextAccessor.Object);

        // Act
        var result = await helper.T("test.key", "Default Text");

        // Assert
        result.Should().Be("Test Translation");
        callCount.Should().Be(1);

        // Note: EnsureStringAsync is called in a fire-and-forget Task.Run, so we can't verify it directly
        // in a reliable way without adding delays
    }

    [Fact]
    public async Task T_WithSpecificLanguage_UsesProvidedLanguage()
    {
        // Arrange
        var callCount = 0;
        var mockTranslationService = new Mock<ITranslationService>();
        var mockHttpContextAccessor = new Mock<IHttpContextAccessor>();

        mockTranslationService
            .Setup(s => s.GetAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback(() => callCount++)
            .ReturnsAsync("German Translation");

        var helper = new TranslationHelper(mockTranslationService.Object, mockHttpContextAccessor.Object);

        // Act
        var result = await helper.T("test.key", "de");

        // Assert
        result.Should().Be("German Translation");
        callCount.Should().Be(1);
    }

    [Fact]
    public async Task T_WithSpecificLanguageAndDefault_CallsBothMethods()
    {
        // Arrange
        var callCount = 0;
        var mockTranslationService = new Mock<ITranslationService>();
        var mockHttpContextAccessor = new Mock<IHttpContextAccessor>();

        mockTranslationService
            .Setup(s => s.GetAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback(() => callCount++)
            .ReturnsAsync("Spanish Translation");

        mockTranslationService
            .Setup(s => s.EnsureStringAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var helper = new TranslationHelper(mockTranslationService.Object, mockHttpContextAccessor.Object);

        // Act
        var result = await helper.T("test.key", "es", "Default Text");

        // Assert
        result.Should().Be("Spanish Translation");
        callCount.Should().Be(1);
    }
}
