using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using mostlylucid.llmtranslate.Models;
using mostlylucid.llmtranslate.Services;
using mostlylucid.llmtranslate.Services.InMemory;
using Xunit;

namespace mostlylucid.llmtranslate.Tests;

public class InMemoryTranslationServiceTests
{
    private static InMemoryTranslationService CreateService(out InMemoryStore store)
    {
        store = new InMemoryStore();
        var ai = new FakeAiProvider();
        var cache = new MemoryCache(new MemoryCacheOptions());
        var req = new RequestTranslationCache();
        var logger = new NullLogger<InMemoryTranslationService>();
        return new InMemoryTranslationService(store, ai, cache, req, logger, enableMemoryCache: true, cacheDurationMinutes: 5);
    }

    [Fact]
    public async Task EnsureString_Then_Get_English_ReturnsDefault()
    {
        // Arrange
        var svc = CreateService(out var store);

        // Act
        await svc.EnsureStringAsync("home.title", "Welcome");
        var text = await svc.GetAsync("home.title", "en");

        // Assert
        text.Should().Be("Welcome");
    }

    [Fact]
    public async Task TranslateAll_Produces_TargetLanguage_Strings()
    {
        // Arrange
        var svc = CreateService(out var store);
        await svc.EnsureStringAsync("home.lead", "Hello world");
        await svc.EnsureStringAsync("home.item1", "Item one");

        var progressEvents = new List<TranslationProgress>();
        var progress = new Progress<TranslationProgress>(p => progressEvents.Add(p));

        // Act
        var count = await svc.TranslateAllStringsAsync("fr", overwriteExisting: true, progress: progress);

        // Assert
        count.Should().Be(2);
        var t1 = await svc.GetAsync("home.lead", "fr");
        var t2 = await svc.GetAsync("home.item1", "fr");
        t1.Should().Contain("<fr>");
        t2.Should().Contain("<fr>");
        progressEvents.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetAsync_NonExistentKey_ReturnsKey()
    {
        // Arrange
        var svc = CreateService(out var store);

        // Act
        var result = await svc.GetAsync("non.existent", "en");

        // Assert
        result.Should().Be("non.existent");
    }

    [Fact]
    public async Task EnsureStringAsync_CreatesEnglishTranslation()
    {
        // Arrange
        var svc = CreateService(out var store);

        // Act
        await svc.EnsureStringAsync("test.key", "Test Value");

        // Assert
        var result = await svc.GetAsync("test.key", "en");
        result.Should().Be("Test Value");
    }

    [Fact]
    public async Task EnsureStringAsync_ExistingKeyWithDifferentText_UpdatesDefaultText()
    {
        // Arrange
        var svc = CreateService(out var store);
        await svc.EnsureStringAsync("test.key", "Original Value");

        // Act
        await svc.EnsureStringAsync("test.key", "New Value");

        // Assert
        var result = await svc.GetAsync("test.key", "en");
        result.Should().Be("New Value", "should update the default text when it changes");
    }

    [Fact]
    public async Task EnsureStringAsync_ExistingKeyWithSameText_KeepsOriginal()
    {
        // Arrange
        var svc = CreateService(out var store);
        await svc.EnsureStringAsync("test.key", "Same Value");

        // Act
        await svc.EnsureStringAsync("test.key", "Same Value");

        // Assert
        var result = await svc.GetAsync("test.key", "en");
        result.Should().Be("Same Value");
    }

    [Fact]
    public async Task TranslateAllStringsAsync_WithProgress_ReportsProgress()
    {
        // Arrange
        var svc = CreateService(out var store);
        await svc.EnsureStringAsync("key1", "Value 1");
        await svc.EnsureStringAsync("key2", "Value 2");
        await svc.EnsureStringAsync("key3", "Value 3");

        var progressEvents = new List<TranslationProgress>();
        var progress = new Progress<TranslationProgress>(p => progressEvents.Add(p));

        // Act
        await svc.TranslateAllStringsAsync("de", overwriteExisting: false, progress: progress);

        // Assert
        progressEvents.Should().NotBeEmpty();
        progressEvents.Should().Contain(p => p.Total > 0);
    }

    [Fact]
    public async Task TranslateAllStringsAsync_OverwriteFalse_SkipsExisting()
    {
        // Arrange
        var svc = CreateService(out var store);
        await svc.EnsureStringAsync("test.key", "Original");

        // Manually add a translation
        await svc.TranslateAllStringsAsync("fr", overwriteExisting: true);
        var firstTranslation = await svc.GetAsync("test.key", "fr");

        // Act - translate again without overwrite
        var count = await svc.TranslateAllStringsAsync("fr", overwriteExisting: false);

        // Assert
        var secondTranslation = await svc.GetAsync("test.key", "fr");
        secondTranslation.Should().Be(firstTranslation, "should not overwrite existing translation");
    }

    [Fact]
    public async Task TranslateAllStringsAsync_NoStrings_ReturnsZero()
    {
        // Arrange
        var svc = CreateService(out var store);

        // Act
        var count = await svc.TranslateAllStringsAsync("fr");

        // Assert
        count.Should().Be(0);
    }

    [Fact]
    public async Task GetAsync_UsesCache_WhenEnabled()
    {
        // Arrange
        var svc = CreateService(out var store);
        await svc.EnsureStringAsync("cached.key", "Cached Value");

        // Act - First call should cache
        var result1 = await svc.GetAsync("cached.key", "en");
        var result2 = await svc.GetAsync("cached.key", "en");

        // Assert
        result1.Should().Be("Cached Value");
        result2.Should().Be("Cached Value");
    }

    [Fact]
    public async Task GetAsync_DifferentLanguages_ReturnsDifferentTranslations()
    {
        // Arrange
        var svc = CreateService(out var store);
        await svc.EnsureStringAsync("multi.lang", "English Text");
        await svc.TranslateAllStringsAsync("fr", overwriteExisting: true);
        await svc.TranslateAllStringsAsync("de", overwriteExisting: true);

        // Act
        var enText = await svc.GetAsync("multi.lang", "en");
        var frText = await svc.GetAsync("multi.lang", "fr");
        var deText = await svc.GetAsync("multi.lang", "de");

        // Assert
        enText.Should().Be("English Text");
        frText.Should().Contain("<fr>");
        deText.Should().Contain("<de>");
        frText.Should().NotBe(deText);
    }

    [Fact]
    public async Task TranslateAllStringsAsync_MultipleKeys_TranslatesAll()
    {
        // Arrange
        var svc = CreateService(out var store);
        var keys = new[] { "key1", "key2", "key3", "key4", "key5" };
        foreach (var key in keys)
        {
            await svc.EnsureStringAsync(key, $"Value for {key}");
        }

        // Act
        var count = await svc.TranslateAllStringsAsync("it", overwriteExisting: true);

        // Assert
        count.Should().Be(keys.Length);
        foreach (var key in keys)
        {
            var translated = await svc.GetAsync(key, "it");
            translated.Should().Contain("<it>");
        }
    }
}

