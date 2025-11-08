using FluentAssertions;
using mostlylucid.activetranslatetag.Helpers;
using Xunit;

namespace mostlylucid.activetranslatetag.Tests;

public class HtmlTextExtractorTests
{
    [Fact]
    public void ExtractText_PlainText_ReturnsUnchanged()
    {
        // Arrange
        var text = "Hello, World!";

        // Act
        var result = HtmlTextExtractor.ExtractText(text);

        // Assert
        result.Should().Be(text);
    }

    [Fact]
    public void ExtractText_SimpleHtml_ReturnsPlainText()
    {
        // Arrange
        var html = "<p>Hello, World!</p>";

        // Act
        var result = HtmlTextExtractor.ExtractText(html);

        // Assert
        result.Should().Be("Hello, World!");
    }

    [Fact]
    public void ExtractText_ComplexHtml_ReturnsPlainText()
    {
        // Arrange
        var html = "<div><h1>Title</h1><p>This is a <strong>paragraph</strong> with <em>formatting</em>.</p></div>";

        // Act
        var result = HtmlTextExtractor.ExtractText(html);

        // Assert
        result.Should().Contain("Title");
        result.Should().Contain("This is a paragraph with formatting");
    }

    [Fact]
    public void ExtractText_RemovesScriptTags()
    {
        // Arrange
        var html = "<p>Visible text</p><script>alert('hidden');</script>";

        // Act
        var result = HtmlTextExtractor.ExtractText(html);

        // Assert
        result.Should().Contain("Visible text");
        result.Should().NotContain("alert");
        result.Should().NotContain("hidden");
    }

    [Fact]
    public void ExtractText_RemovesStyleTags()
    {
        // Arrange
        var html = "<p>Visible text</p><style>.hidden { display: none; }</style>";

        // Act
        var result = HtmlTextExtractor.ExtractText(html);

        // Assert
        result.Should().Contain("Visible text");
        result.Should().NotContain("display");
        result.Should().NotContain("hidden");
    }

    [Fact]
    public void ExtractText_DecodesHtmlEntities()
    {
        // Arrange
        var html = "<p>Hello &amp; goodbye &lt;tag&gt;</p>";

        // Act
        var result = HtmlTextExtractor.ExtractText(html);

        // Assert
        result.Should().Contain("Hello & goodbye <tag>");
    }

    [Fact]
    public void ContainsHtml_PlainText_ReturnsFalse()
    {
        // Arrange
        var text = "Hello, World!";

        // Act
        var result = HtmlTextExtractor.ContainsHtml(text);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void ContainsHtml_WithHtml_ReturnsTrue()
    {
        // Arrange
        var html = "<p>Hello</p>";

        // Act
        var result = HtmlTextExtractor.ContainsHtml(html);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void StripHtmlForTranslation_PlainText_ReturnsUnchanged()
    {
        // Arrange
        var text = "Hello, World!";

        // Act
        var result = HtmlTextExtractor.StripHtmlForTranslation(text);

        // Assert
        result.CleanText.Should().Be(text);
        result.TagMap.Should().BeEmpty();
    }

    [Fact]
    public void StripHtmlForTranslation_WithHtml_ReturnsCleanTextAndTagMap()
    {
        // Arrange
        var html = "<p>Hello, <strong>World</strong>!</p>";

        // Act
        var result = HtmlTextExtractor.StripHtmlForTranslation(html);

        // Assert
        // The clean text should contain the content but with placeholder tags
        result.CleanText.Should().Contain("Hello");
        result.CleanText.Should().Contain("World");
        result.TagMap.Should().NotBeEmpty();
        result.TagMap.Should().HaveCount(4); // <p>, <strong>, </strong>, </p>
    }

    [Fact]
    public void RestoreHtmlAfterTranslation_RestoresOriginalStructure()
    {
        // Arrange
        var html = "<p>Hello, <strong>World</strong>!</p>";
        var stripped = HtmlTextExtractor.StripHtmlForTranslation(html);

        // Simulate translation (change text but keep placeholders)
        var translated = stripped.CleanText.Replace("Hello", "Bonjour").Replace("World", "Monde");

        // Act
        var restored = HtmlTextExtractor.RestoreHtmlAfterTranslation(translated, stripped.TagMap);

        // Assert
        restored.Should().Contain("<p>");
        restored.Should().Contain("<strong>");
        restored.Should().Contain("Bonjour");
        restored.Should().Contain("Monde");
    }

    [Fact]
    public void ExtractWithPlaceholders_SimpleHtml_ReturnsPlainTextAndPlaceholders()
    {
        // Arrange
        var html = "<p>Hello <img src='test.jpg' /> World</p>";

        // Act
        var result = HtmlTextExtractor.ExtractWithPlaceholders(html);

        // Assert
        result.PlainText.Should().Contain("Hello");
        result.PlainText.Should().Contain("World");
        result.Placeholders.Should().NotBeEmpty();
    }

    [Fact]
    public void ReinjectHtml_RestoresOriginalStructure()
    {
        // Arrange
        var html = "<p>Hello <img src='test.jpg' /> World</p>";
        var extracted = HtmlTextExtractor.ExtractWithPlaceholders(html);

        // Simulate translation
        var translated = extracted.PlainText.Replace("Hello", "Bonjour").Replace("World", "Monde");

        // Act
        var restored = HtmlTextExtractor.ReinjectHtml(translated, extracted.Placeholders);

        // Assert
        restored.Should().Contain("Bonjour");
        restored.Should().Contain("Monde");
        restored.Should().Contain("<img");
    }

    [Fact]
    public void ExtractText_WithLineBreaks_PreservesStructure()
    {
        // Arrange
        var html = "<p>First paragraph</p><p>Second paragraph</p>";

        // Act
        var result = HtmlTextExtractor.ExtractText(html);

        // Assert
        result.Should().Contain("First paragraph");
        result.Should().Contain("Second paragraph");
    }

    [Fact]
    public void ExtractText_EmptyString_ReturnsEmpty()
    {
        // Act
        var result = HtmlTextExtractor.ExtractText("");

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void ExtractText_Null_ReturnsEmpty()
    {
        // Act
        var result = HtmlTextExtractor.ExtractText(null!);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void StripHtmlForTranslation_ComplexHtml_PreservesAllTags()
    {
        // Arrange
        var html = "<div class='container'><h1>Title</h1><p>Text with <a href='#'>link</a></p></div>";

        // Act
        var result = HtmlTextExtractor.StripHtmlForTranslation(html);

        // Assert
        // Clean text should contain the visible text content
        result.CleanText.Should().Contain("Title");
        result.CleanText.Should().Contain("Text with");
        result.CleanText.Should().Contain("link");
        result.TagMap.Should().NotBeEmpty();

        // Restore should work
        var restored = HtmlTextExtractor.RestoreHtmlAfterTranslation(result.CleanText, result.TagMap);
        restored.Should().Contain("<div");
        restored.Should().Contain("<h1>");
        restored.Should().Contain("<a href=");
    }

    [Fact]
    public void ExtractWithPlaceholders_NoHtml_ReturnsOriginalText()
    {
        // Arrange
        var text = "Plain text without HTML";

        // Act
        var result = HtmlTextExtractor.ExtractWithPlaceholders(text);

        // Assert
        result.PlainText.Should().Be(text);
        result.Placeholders.Should().BeEmpty();
    }

    [Fact]
    public void ReinjectHtml_NoPlaceholders_ReturnsOriginal()
    {
        // Arrange
        var text = "Plain text";

        // Act
        var result = HtmlTextExtractor.ReinjectHtml(text, new List<HtmlPlaceholder>());

        // Assert
        result.Should().Be(text);
    }
}
