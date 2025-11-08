using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using mostlylucid.llmtranslate.Services;
using mostlylucid.llmtranslate.Services.Providers;
using Xunit;

namespace mostlylucid.llmtranslate.Tests;

public class ChunkingAiTranslationProviderTests
{
    [Fact]
    public async Task TranslateAsync_ShortText_CallsInnerProviderDirectly()
    {
        // Arrange
        var mockInner = new Mock<IAiTranslationProvider>();
        mockInner.Setup(x => x.TranslateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string text, string target, string source, CancellationToken ct) => $"{text}_translated");

        var logger = new NullLogger<ChunkingAiTranslationProvider>();
        var provider = new ChunkingAiTranslationProvider(mockInner.Object, chunkLength: 100, overlap: 10, logger);

        var shortText = "Hello";

        // Act
        var result = await provider.TranslateAsync(shortText, "fr", "en");

        // Assert
        result.Should().Be("Hello_translated");
        mockInner.Verify(x => x.TranslateAsync(shortText, "fr", "en", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task TranslateAsync_LongText_SplitsIntoChunks()
    {
        // Arrange
        var mockInner = new Mock<IAiTranslationProvider>();
        var callCount = 0;
        mockInner.Setup(x => x.TranslateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string text, string target, string source, CancellationToken ct) =>
            {
                callCount++;
                return $"[CHUNK_{callCount}]";
            });

        var logger = new NullLogger<ChunkingAiTranslationProvider>();
        var provider = new ChunkingAiTranslationProvider(mockInner.Object, chunkLength: 20, overlap: 5, logger);

        var longText = "This is a very long text that needs to be split into multiple chunks for translation.";

        // Act
        var result = await provider.TranslateAsync(longText, "fr", "en");

        // Assert
        result.Should().NotBeNullOrEmpty();
        mockInner.Verify(x => x.TranslateAsync(It.IsAny<string>(), "fr", "en", It.IsAny<CancellationToken>()), Times.AtLeast(2));
    }

    [Fact]
    public async Task TranslateAsync_EmptyText_ReturnsEmpty()
    {
        // Arrange
        var mockInner = new Mock<IAiTranslationProvider>();
        var logger = new NullLogger<ChunkingAiTranslationProvider>();
        var provider = new ChunkingAiTranslationProvider(mockInner.Object, chunkLength: 100, overlap: 10, logger);

        // Act
        var result = await provider.TranslateAsync("", "fr", "en");

        // Assert
        result.Should().Be("");
        mockInner.Verify(x => x.TranslateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task TranslateBatchAsync_MixedSizes_SeparatesSmallAndLarge()
    {
        // Arrange
        var mockInner = new Mock<IAiTranslationProvider>();
        mockInner.Setup(x => x.TranslateBatchAsync(It.IsAny<Dictionary<string, string>>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Dictionary<string, string> items, string target, string source, CancellationToken ct) =>
                items.ToDictionary(kv => kv.Key, kv => $"{kv.Value}_batch_translated"));

        mockInner.Setup(x => x.TranslateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string text, string target, string source, CancellationToken ct) => $"{text}_individual_translated");

        var logger = new NullLogger<ChunkingAiTranslationProvider>();
        var provider = new ChunkingAiTranslationProvider(mockInner.Object, chunkLength: 20, overlap: 5, logger);

        var items = new Dictionary<string, string>
        {
            { "short1", "Hi" },
            { "short2", "Hello" },
            { "long1", "This is a very long text that exceeds the chunk length limit" }
        };

        // Act
        var result = await provider.TranslateBatchAsync(items, "fr", "en");

        // Assert
        result.Should().HaveCount(3);
        result["short1"].Should().Be("Hi_batch_translated");
        result["short2"].Should().Be("Hello_batch_translated");
        result["long1"].Should().NotBeNullOrEmpty();

        // Verify batch was called for small items
        mockInner.Verify(x => x.TranslateBatchAsync(
            It.Is<Dictionary<string, string>>(d => d.ContainsKey("short1") && d.ContainsKey("short2")),
            "fr", "en", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task TranslateBatchAsync_AllSmallItems_UsesBatchOnly()
    {
        // Arrange
        var mockInner = new Mock<IAiTranslationProvider>();
        mockInner.Setup(x => x.TranslateBatchAsync(It.IsAny<Dictionary<string, string>>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Dictionary<string, string> items, string target, string source, CancellationToken ct) =>
                items.ToDictionary(kv => kv.Key, kv => $"{kv.Value}_translated"));

        var logger = new NullLogger<ChunkingAiTranslationProvider>();
        var provider = new ChunkingAiTranslationProvider(mockInner.Object, chunkLength: 100, overlap: 10, logger);

        var items = new Dictionary<string, string>
        {
            { "key1", "Short 1" },
            { "key2", "Short 2" },
            { "key3", "Short 3" }
        };

        // Act
        var result = await provider.TranslateBatchAsync(items, "fr", "en");

        // Assert
        result.Should().HaveCount(3);
        mockInner.Verify(x => x.TranslateBatchAsync(It.IsAny<Dictionary<string, string>>(), "fr", "en", It.IsAny<CancellationToken>()), Times.Once);
        mockInner.Verify(x => x.TranslateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task TranslateBatchAsync_EmptyDictionary_ReturnsEmpty()
    {
        // Arrange
        var mockInner = new Mock<IAiTranslationProvider>();
        var logger = new NullLogger<ChunkingAiTranslationProvider>();
        var provider = new ChunkingAiTranslationProvider(mockInner.Object, chunkLength: 100, overlap: 10, logger);

        // Act
        var result = await provider.TranslateBatchAsync(new Dictionary<string, string>(), "fr", "en");

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void Constructor_NegativeChunkLength_UsesMinimumValue()
    {
        // Arrange
        var mockInner = new Mock<IAiTranslationProvider>();
        var logger = new NullLogger<ChunkingAiTranslationProvider>();

        // Act - constructor should handle negative value
        var provider = new ChunkingAiTranslationProvider(mockInner.Object, chunkLength: -100, overlap: 10, logger);

        // Assert - should not throw
        provider.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_NegativeOverlap_UsesZero()
    {
        // Arrange
        var mockInner = new Mock<IAiTranslationProvider>();
        var logger = new NullLogger<ChunkingAiTranslationProvider>();

        // Act - constructor should handle negative value
        var provider = new ChunkingAiTranslationProvider(mockInner.Object, chunkLength: 100, overlap: -10, logger);

        // Assert - should not throw
        provider.Should().NotBeNull();
    }

    [Fact]
    public async Task TranslateAsync_TextExactlyAtChunkLength_NoChunking()
    {
        // Arrange
        var mockInner = new Mock<IAiTranslationProvider>();
        mockInner.Setup(x => x.TranslateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string text, string target, string source, CancellationToken ct) => $"{text}_translated");

        var logger = new NullLogger<ChunkingAiTranslationProvider>();
        var provider = new ChunkingAiTranslationProvider(mockInner.Object, chunkLength: 10, overlap: 2, logger);

        var text = "1234567890"; // Exactly 10 characters

        // Act
        var result = await provider.TranslateAsync(text, "fr", "en");

        // Assert
        result.Should().Be("1234567890_translated");
        mockInner.Verify(x => x.TranslateAsync(text, "fr", "en", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task TranslateAsync_NullText_ReturnsNull()
    {
        // Arrange
        var mockInner = new Mock<IAiTranslationProvider>();
        var logger = new NullLogger<ChunkingAiTranslationProvider>();
        var provider = new ChunkingAiTranslationProvider(mockInner.Object, chunkLength: 100, overlap: 10, logger);

        // Act
        var result = await provider.TranslateAsync(null!, "fr", "en");

        // Assert
        result.Should().BeNull();
    }
}
