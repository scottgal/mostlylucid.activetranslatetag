using FluentAssertions;
using mostlylucid.activetranslatetag.Helpers;
using Xunit;

namespace mostlylucid.activetranslatetag.Tests;

public class ContentHashTests
{
    [Fact]
    public void Generate_SameContent_ProducesSameHash()
    {
        // Arrange
        var content = "Hello, world!";

        // Act
        var hash1 = ContentHash.Generate(content);
        var hash2 = ContentHash.Generate(content);

        // Assert
        hash1.Should().Be(hash2);
    }

    [Fact]
    public void Generate_DifferentContent_ProducesDifferentHash()
    {
        // Arrange
        var content1 = "Hello, world!";
        var content2 = "Goodbye, world!";

        // Act
        var hash1 = ContentHash.Generate(content1);
        var hash2 = ContentHash.Generate(content2);

        // Assert
        hash1.Should().NotBe(hash2);
    }

    [Fact]
    public void Generate_EmptyString_ProducesHash()
    {
        // Act
        var hash = ContentHash.Generate("");

        // Assert
        hash.Should().NotBeNullOrEmpty();
        hash.Should().HaveLength(16); // 8 bytes = 16 hex chars
    }

    [Fact]
    public void Generate_NullString_TreatedAsEmpty()
    {
        // Act
        var hashNull = ContentHash.Generate(null);
        var hashEmpty = ContentHash.Generate("");

        // Assert
        hashNull.Should().Be(hashEmpty);
    }

    [Fact]
    public void Generate_WithBytesToTake_ProducesCorrectLength()
    {
        // Arrange
        var content = "Hello, world!";

        // Act
        var hash1 = ContentHash.Generate(content, 1);
        var hash2 = ContentHash.Generate(content, 4);
        var hash3 = ContentHash.Generate(content, 8);

        // Assert
        hash1.Should().HaveLength(2);  // 1 byte = 2 hex chars
        hash2.Should().HaveLength(8);  // 4 bytes = 8 hex chars
        hash3.Should().HaveLength(16); // 8 bytes = 16 hex chars
    }

    [Fact]
    public void Generate_DefaultBytesToTake_Returns8Bytes()
    {
        // Arrange
        var content = "Hello, world!";

        // Act
        var hash = ContentHash.Generate(content);

        // Assert
        hash.Should().HaveLength(16); // 8 bytes = 16 hex chars
    }

    [Fact]
    public void Generate_AllLowercase_ReturnsLowercaseHex()
    {
        // Arrange
        var content = "Hello, world!";

        // Act
        var hash = ContentHash.Generate(content);

        // Assert
        hash.Should().MatchRegex("^[0-9a-f]+$", "hash should be lowercase hex");
    }

    [Fact]
    public void Generate_InvalidBytesToTake_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var content = "Hello, world!";

        // Act & Assert
        var act1 = () => ContentHash.Generate(content, 0);
        var act2 = () => ContentHash.Generate(content, 9);
        var act3 = () => ContentHash.Generate(content, -1);

        act1.Should().Throw<ArgumentOutOfRangeException>();
        act2.Should().Throw<ArgumentOutOfRangeException>();
        act3.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Generate_LongContent_ProducesConsistentHash()
    {
        // Arrange
        var longContent = string.Join(" ", Enumerable.Repeat("Lorem ipsum dolor sit amet", 100));

        // Act
        var hash1 = ContentHash.Generate(longContent);
        var hash2 = ContentHash.Generate(longContent);

        // Assert
        hash1.Should().Be(hash2);
        hash1.Should().HaveLength(16);
    }

    [Fact]
    public void Generate_SpecialCharacters_HandlesCorrectly()
    {
        // Arrange
        var content = "Hello! @#$%^&*() 你好 مرحبا";

        // Act
        var hash1 = ContentHash.Generate(content);
        var hash2 = ContentHash.Generate(content);

        // Assert
        hash1.Should().Be(hash2);
        hash1.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Generate_MinorContentChange_SignificantlyChangesHash()
    {
        // Arrange
        var content1 = "Hello, world!";
        var content2 = "Hello, World!"; // Capital W

        // Act
        var hash1 = ContentHash.Generate(content1);
        var hash2 = ContentHash.Generate(content2);

        // Assert
        hash1.Should().NotBe(hash2, "even minor changes should produce different hashes");
    }

    [Fact]
    public void Generate_ShorterHash_IsPrefixOfLongerHash()
    {
        // Arrange
        var content = "Hello, world!";

        // Act
        var hash2 = ContentHash.Generate(content, 2);
        var hash8 = ContentHash.Generate(content, 8);

        // Assert
        hash8.Should().StartWith(hash2, "shorter hash should be prefix of longer hash from same content");
    }
}
