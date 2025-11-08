using FluentAssertions;
using Microsoft.Extensions.Configuration;
using mostlylucid.activetranslatetag.Configuration;
using Xunit;

namespace mostlylucid.activetranslatetag.Tests;

public class ConfigurationTests
{
    [Fact]
    public void AiOptions_DefaultValues_AreSetCorrectly()
    {
        // Arrange & Act
        var options = new AiOptions();

        // Assert
        options.DefaultProvider.Should().BeNull();
        options.Chunking.Should().NotBeNull();
        options.OllamaProviders.Should().BeEmpty();
        options.EasyNmtProviders.Should().BeEmpty();
        options.OpenAiProviders.Should().BeEmpty();
    }

    [Fact]
    public void ChunkingOptions_DefaultValues_AreSetCorrectly()
    {
        // Arrange & Act
        var options = new ChunkingOptions();

        // Assert
        options.Enabled.Should().BeFalse();
        options.ChunkLength.Should().Be(800);
        options.Overlap.Should().Be(0);
    }

    [Fact]
    public void EasyNmtProviderOptions_DefaultValues_AreSetCorrectly()
    {
        // Arrange & Act
        var options = new EasyNmtProviderOptions();

        // Assert
        options.Name.Should().Be("default");
        options.BaseUrl.Should().Be("http://localhost:24080/");
    }

    [Fact]
    public void OllamaProviderOptions_DefaultValues_AreSetCorrectly()
    {
        // Arrange & Act
        var options = new OllamaProviderOptions();

        // Assert
        options.Name.Should().Be("ollama");
        options.BaseUrl.Should().Be("http://localhost:11434/");
        options.Model.Should().Be("llama3.1");
    }

    [Fact]
    public void OpenAiProviderOptions_DefaultValues_AreSetCorrectly()
    {
        // Arrange & Act
        var options = new OpenAiProviderOptions();

        // Assert
        options.Name.Should().Be("openai");
        options.ApiKey.Should().BeNull();
        options.Model.Should().Be("gpt-4o-mini");
        options.BaseUrl.Should().BeNull();
    }

    [Fact]
    public void LlmTranslateOptions_DefaultValues_AreSetCorrectly()
    {
        // Arrange & Act
        var options = new LlmTranslateOptions();

        // Assert
        options.Storage.Should().NotBeNull();
        options.Ai.Should().NotBeNull();
        options.Reserved.Should().BeFalse();
    }

    [Fact]
    public void TranslationStorageOptions_DefaultValues_AreSetCorrectly()
    {
        // Arrange & Act
        var options = new TranslationStorageOptions();

        // Assert
        options.StorageType.Should().Be(TranslationStorageType.PostgreSql);
        options.ConnectionString.Should().BeNull();
        options.PostgreSqlSchema.Should().Be("public");
        options.JsonFilePath.Should().BeNull();
        options.JsonAutoSave.Should().BeTrue();
        options.EnableMemoryCache.Should().BeTrue();
        options.MemoryCacheDurationMinutes.Should().Be(60);
    }

    [Fact]
    public void AiOptions_CanSetProperties()
    {
        // Arrange
        var options = new AiOptions
        {
            DefaultProvider = "ollama",
            Chunking = new ChunkingOptions { Enabled = true, ChunkLength = 500, Overlap = 50 },
            OllamaProviders = new[] { new OllamaProviderOptions { Name = "test", BaseUrl = "http://test:11434/" } }
        };

        // Assert
        options.DefaultProvider.Should().Be("ollama");
        options.Chunking.Enabled.Should().BeTrue();
        options.Chunking.ChunkLength.Should().Be(500);
        options.Chunking.Overlap.Should().Be(50);
        options.OllamaProviders.Should().HaveCount(1);
        options.OllamaProviders[0].Name.Should().Be("test");
    }

    [Fact]
    public void LlmTranslateOptions_CanBindFromConfiguration()
    {
        // Arrange
        var configData = new Dictionary<string, string?>
        {
            ["LlmTranslate:Storage:StorageType"] = "JsonFile",
            ["LlmTranslate:Storage:JsonFilePath"] = "./translations.json",
            ["LlmTranslate:Storage:EnableMemoryCache"] = "true",
            ["LlmTranslate:Ai:DefaultProvider"] = "ollama",
            ["LlmTranslate:Ai:Chunking:Enabled"] = "true",
            ["LlmTranslate:Ai:Chunking:ChunkLength"] = "1000",
            ["LlmTranslate:Ai:OllamaProviders:0:Name"] = "ollama",
            ["LlmTranslate:Ai:OllamaProviders:0:BaseUrl"] = "http://localhost:11434/",
            ["LlmTranslate:Ai:OllamaProviders:0:Model"] = "llama3.1"
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        var options = new LlmTranslateOptions();

        // Act
        configuration.GetSection("LlmTranslate").Bind(options);

        // Assert
        options.Storage.StorageType.Should().Be(TranslationStorageType.JsonFile);
        options.Storage.JsonFilePath.Should().Be("./translations.json");
        options.Storage.EnableMemoryCache.Should().BeTrue();
        options.Ai.DefaultProvider.Should().Be("ollama");
        options.Ai.Chunking.Enabled.Should().BeTrue();
        options.Ai.Chunking.ChunkLength.Should().Be(1000);
        options.Ai.OllamaProviders.Should().HaveCount(1);
        options.Ai.OllamaProviders[0].Name.Should().Be("ollama");
        options.Ai.OllamaProviders[0].Model.Should().Be("llama3.1");
    }

    [Fact]
    public void LlmTranslateOptions_CanBindMultipleProviders()
    {
        // Arrange
        var configData = new Dictionary<string, string?>
        {
            ["LlmTranslate:Ai:OllamaProviders:0:Name"] = "ollama1",
            ["LlmTranslate:Ai:OllamaProviders:0:BaseUrl"] = "http://localhost:11434/",
            ["LlmTranslate:Ai:OllamaProviders:1:Name"] = "ollama2",
            ["LlmTranslate:Ai:OllamaProviders:1:BaseUrl"] = "http://remote:11434/",
            ["LlmTranslate:Ai:EasyNmtProviders:0:Name"] = "easynmt1",
            ["LlmTranslate:Ai:EasyNmtProviders:0:BaseUrl"] = "http://localhost:24080/"
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        var options = new LlmTranslateOptions();

        // Act
        configuration.GetSection("LlmTranslate").Bind(options);

        // Assert
        options.Ai.OllamaProviders.Should().HaveCount(2);
        options.Ai.OllamaProviders[0].Name.Should().Be("ollama1");
        options.Ai.OllamaProviders[1].Name.Should().Be("ollama2");
        options.Ai.EasyNmtProviders.Should().HaveCount(1);
        options.Ai.EasyNmtProviders[0].Name.Should().Be("easynmt1");
    }

    [Fact]
    public void TranslationStorageOptions_PostgreSqlConfiguration()
    {
        // Arrange & Act
        var options = new TranslationStorageOptions
        {
            StorageType = TranslationStorageType.PostgreSql,
            ConnectionString = "Host=localhost;Database=translations;Username=user;Password=pass",
            PostgreSqlSchema = "custom_schema"
        };

        // Assert
        options.StorageType.Should().Be(TranslationStorageType.PostgreSql);
        options.ConnectionString.Should().NotBeNullOrEmpty();
        options.PostgreSqlSchema.Should().Be("custom_schema");
    }

    [Fact]
    public void TranslationStorageOptions_SqliteConfiguration()
    {
        // Arrange & Act
        var options = new TranslationStorageOptions
        {
            StorageType = TranslationStorageType.Sqlite,
            ConnectionString = "Data Source=translations.db"
        };

        // Assert
        options.StorageType.Should().Be(TranslationStorageType.Sqlite);
        options.ConnectionString.Should().Be("Data Source=translations.db");
    }

    [Fact]
    public void TranslationStorageOptions_JsonFileConfiguration()
    {
        // Arrange & Act
        var options = new TranslationStorageOptions
        {
            StorageType = TranslationStorageType.JsonFile,
            JsonFilePath = "./translations.json",
            JsonAutoSave = false
        };

        // Assert
        options.StorageType.Should().Be(TranslationStorageType.JsonFile);
        options.JsonFilePath.Should().Be("./translations.json");
        options.JsonAutoSave.Should().BeFalse();
    }
}
