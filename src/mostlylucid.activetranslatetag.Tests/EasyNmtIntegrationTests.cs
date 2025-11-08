using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using mostlylucid.activetranslatetag.Services.Providers;
using Xunit;

namespace mostlylucid.activetranslatetag.Tests;

/// <summary>
/// Integration tests for EasyNMT provider against a live EasyNMT server at localhost:24080
/// </summary>
public class EasyNmtIntegrationTests
{
    private readonly EasyNmtTranslationProvider _provider;

    public EasyNmtIntegrationTests()
    {
        var httpClient = new HttpClient();
        var logger = new NullLogger<EasyNmtTranslationProvider>();
        _provider = new EasyNmtTranslationProvider(httpClient, logger, "http://localhost:24080/");
    }

    [Fact]
    public async Task TranslateAsync_SingleText_ReturnsTranslation()
    {
        // Arrange
        var text = "Hello, world!";
        var targetLanguage = "fr";

        // Act
        var result = await _provider.TranslateAsync(text, targetLanguage, "en");

        // Assert
        result.Should().NotBeNullOrEmpty();
        result.Should().NotBe(text, "translation should differ from original");
        Console.WriteLine($"Original: {text}");
        Console.WriteLine($"Translation (fr): {result}");
    }

    [Fact]
    public async Task TranslateAsync_EnglishToGerman_ReturnsGermanText()
    {
        // Arrange
        var text = "Good morning, how are you?";
        var targetLanguage = "de";

        // Act
        var result = await _provider.TranslateAsync(text, targetLanguage, "en");

        // Assert
        result.Should().NotBeNullOrEmpty();
        result.Should().NotBe(text);
        Console.WriteLine($"Original: {text}");
        Console.WriteLine($"Translation (de): {result}");
    }

    [Fact]
    public async Task TranslateAsync_EnglishToSpanish_ReturnsSpanishText()
    {
        // Arrange
        var text = "Thank you very much";
        var targetLanguage = "es";

        // Act
        var result = await _provider.TranslateAsync(text, targetLanguage, "en");

        // Assert
        result.Should().NotBeNullOrEmpty();
        result.Should().NotBe(text);
        Console.WriteLine($"Original: {text}");
        Console.WriteLine($"Translation (es): {result}");
    }

    [Fact]
    public async Task TranslateAsync_LongText_ReturnsTranslation()
    {
        // Arrange
        var text = "This is a longer text that contains multiple sentences. " +
                   "The translation system should be able to handle this properly. " +
                   "We want to ensure that longer texts work correctly.";
        var targetLanguage = "fr";

        // Act
        var result = await _provider.TranslateAsync(text, targetLanguage, "en");

        // Assert
        result.Should().NotBeNullOrEmpty();
        result.Should().NotBe(text);
        Console.WriteLine($"Original: {text}");
        Console.WriteLine($"Translation (fr): {result}");
    }

    [Fact]
    public async Task TranslateBatchAsync_MultipleTexts_ReturnsAllTranslations()
    {
        // Arrange
        var items = new Dictionary<string, string>
        {
            { "greeting", "Hello" },
            { "farewell", "Goodbye" },
            { "thanks", "Thank you" }
        };
        var targetLanguage = "it";

        // Act
        var result = await _provider.TranslateBatchAsync(items, targetLanguage, "en");

        // Assert
        result.Should().HaveCount(3);
        result.Should().ContainKeys("greeting", "farewell", "thanks");
        result.Values.Should().AllSatisfy(v => v.Should().NotBeNullOrEmpty());

        Console.WriteLine("Batch translation results (it):");
        foreach (var kvp in result)
        {
            Console.WriteLine($"  {kvp.Key}: {items[kvp.Key]} -> {kvp.Value}");
        }
    }

    [Fact]
    public async Task TranslateBatchAsync_EmptyDictionary_ReturnsEmptyDictionary()
    {
        // Arrange
        var items = new Dictionary<string, string>();
        var targetLanguage = "fr";

        // Act
        var result = await _provider.TranslateBatchAsync(items, targetLanguage, "en");

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task TranslateAsync_EmptyString_ReturnsOriginal()
    {
        // Arrange
        var text = "";
        var targetLanguage = "fr";

        // Act
        var result = await _provider.TranslateAsync(text, targetLanguage, "en");

        // Assert
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task TranslateAsync_SpecialCharacters_HandlesCorrectly()
    {
        // Arrange
        var text = "Hello! How are you? I'm fine, thanks.";
        var targetLanguage = "de";

        // Act
        var result = await _provider.TranslateAsync(text, targetLanguage, "en");

        // Assert
        result.Should().NotBeNullOrEmpty();
        result.Should().NotBe(text);
        Console.WriteLine($"Original: {text}");
        Console.WriteLine($"Translation (de): {result}");
    }

    [Fact]
    public async Task TranslateBatchAsync_LargerBatch_HandlesCorrectly()
    {
        // Arrange
        var items = new Dictionary<string, string>
        {
            { "item1", "Welcome to our application" },
            { "item2", "Please enter your username" },
            { "item3", "Password is required" },
            { "item4", "Login successful" },
            { "item5", "An error occurred" },
            { "item6", "Please try again" },
            { "item7", "Settings" },
            { "item8", "Logout" }
        };
        var targetLanguage = "fr";

        // Act
        var result = await _provider.TranslateBatchAsync(items, targetLanguage, "en");

        // Assert
        result.Should().HaveCount(8);
        result.Values.Should().AllSatisfy(v => v.Should().NotBeNullOrEmpty());

        Console.WriteLine("Large batch translation results (fr):");
        foreach (var kvp in result)
        {
            Console.WriteLine($"  {kvp.Key}: {items[kvp.Key]} -> {kvp.Value}");
        }
    }
}
