using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using mostlylucid.llmtranslate.Configuration;
using mostlylucid.llmtranslate.Extensions;
using mostlylucid.llmtranslate.Services;
using mostlylucid.llmtranslate.Services.InMemory;
using Xunit;

namespace mostlylucid.llmtranslate.Tests;

public class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddAutoTranslateInMemory_RegistersServices()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        // Need to add a fake AI provider first
        services.AddScoped<IAiTranslationProvider, FakeAiProvider>();

        // Act
        services.AddAutoTranslateInMemory();
        var provider = services.BuildServiceProvider();

        // Assert
        var translationService = provider.GetService<ITranslationService>();
        translationService.Should().NotBeNull();
        translationService.Should().BeOfType<InMemoryTranslationService>();
    }

    [Fact]
    public void AddAutoTranslateWithJsonFile_RegistersServices()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        var tempFile = Path.GetTempFileName();

        // Need to add a fake AI provider first
        services.AddScoped<IAiTranslationProvider, FakeAiProvider>();

        try
        {
            // Act
            services.AddAutoTranslateWithJsonFile(tempFile);
            var provider = services.BuildServiceProvider();

            // Assert
            var translationService = provider.GetService<ITranslationService>();
            translationService.Should().NotBeNull();
            translationService.Should().BeOfType<JsonFileTranslationService>();
        }
        finally
        {
            // Cleanup
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [Fact]
    public void AddAutoTranslateWithSqlite_RegistersServices()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        var connectionString = "Data Source=:memory:";

        // Need to add a fake AI provider first
        services.AddScoped<IAiTranslationProvider, FakeAiProvider>();

        // Act
        services.AddAutoTranslateWithSqlite(connectionString);
        var provider = services.BuildServiceProvider();

        // Assert
        var translationService = provider.GetService<ITranslationService>();
        translationService.Should().NotBeNull();
        translationService.Should().BeOfType<EfTranslationService>();
    }

    [Fact]
    public void AddAutoTranslate_WithActionConfiguration_RegistersServices()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddScoped<IAiTranslationProvider, FakeAiProvider>();

        // Act
        services.AddAutoTranslate(options =>
        {
            options.StorageType = TranslationStorageType.InMemory;
        });
        var provider = services.BuildServiceProvider();

        // Assert
        var translationService = provider.GetService<ITranslationService>();
        translationService.Should().NotBeNull();
    }

    [Fact]
    public void AddAutoTranslate_JsonFileWithoutPath_ThrowsArgumentException()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddScoped<IAiTranslationProvider, FakeAiProvider>();

        // Act
        var act = () => services.AddAutoTranslate(options =>
        {
            options.StorageType = TranslationStorageType.JsonFile;
            options.JsonFilePath = null;
        }).BuildServiceProvider();

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*JsonFilePath*");
    }

    [Fact]
    public void AddAutoTranslate_PostgreSqlWithoutConnectionString_ThrowsArgumentException()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddScoped<IAiTranslationProvider, FakeAiProvider>();

        // Act
        var act = () => services.AddAutoTranslate(options =>
        {
            options.StorageType = TranslationStorageType.PostgreSql;
            options.ConnectionString = null;
        }).BuildServiceProvider();

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*ConnectionString*");
    }

    [Fact]
    public void AddAutoTranslateFromConfiguration_WithOllamaProvider_RegistersProvider()
    {
        // Arrange
        var services = new ServiceCollection();
        var configData = new Dictionary<string, string?>
        {
            ["LlmTranslate:Storage:StorageType"] = "InMemory",
            ["LlmTranslate:Ai:DefaultProvider"] = "ollama",
            ["LlmTranslate:Ai:OllamaProviders:0:Name"] = "ollama",
            ["LlmTranslate:Ai:OllamaProviders:0:BaseUrl"] = "http://localhost:11434/",
            ["LlmTranslate:Ai:OllamaProviders:0:Model"] = "llama3.1"
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        // Act
        services.AddAutoTranslateFromConfiguration(configuration);
        var provider = services.BuildServiceProvider();

        // Assert
        var aiProvider = provider.GetService<IAiTranslationProvider>();
        aiProvider.Should().NotBeNull();
    }

    [Fact]
    public void AddAutoTranslateFromConfiguration_WithEasyNmtProvider_RegistersProvider()
    {
        // Arrange
        var services = new ServiceCollection();
        var configData = new Dictionary<string, string?>
        {
            ["LlmTranslate:Storage:StorageType"] = "InMemory",
            ["LlmTranslate:Ai:DefaultProvider"] = "easynmt",
            ["LlmTranslate:Ai:EasyNmtProviders:0:Name"] = "easynmt",
            ["LlmTranslate:Ai:EasyNmtProviders:0:BaseUrl"] = "http://localhost:24080/"
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        // Act
        services.AddAutoTranslateFromConfiguration(configuration);
        var provider = services.BuildServiceProvider();

        // Assert
        var aiProvider = provider.GetService<IAiTranslationProvider>();
        aiProvider.Should().NotBeNull();
    }

    [Fact]
    public void AddAutoTranslateFromConfiguration_WithChunking_RegistersChunkingProvider()
    {
        // Arrange
        var services = new ServiceCollection();
        var configData = new Dictionary<string, string?>
        {
            ["LlmTranslate:Storage:StorageType"] = "InMemory",
            ["LlmTranslate:Ai:DefaultProvider"] = "ollama",
            ["LlmTranslate:Ai:Chunking:Enabled"] = "true",
            ["LlmTranslate:Ai:Chunking:ChunkLength"] = "500",
            ["LlmTranslate:Ai:OllamaProviders:0:Name"] = "ollama",
            ["LlmTranslate:Ai:OllamaProviders:0:BaseUrl"] = "http://localhost:11434/"
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        // Act
        services.AddAutoTranslateFromConfiguration(configuration);
        var provider = services.BuildServiceProvider();

        // Assert
        var aiProvider = provider.GetService<IAiTranslationProvider>();
        aiProvider.Should().NotBeNull();
        aiProvider.Should().BeOfType<mostlylucid.llmtranslate.Services.Providers.ChunkingAiTranslationProvider>();
    }

    [Fact]
    public void AddAutoTranslateFromConfiguration_NoProviderConfigured_ThrowsException()
    {
        // Arrange
        var services = new ServiceCollection();
        var configData = new Dictionary<string, string?>
        {
            ["LlmTranslate:Storage:StorageType"] = "InMemory"
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        // Act
        services.AddAutoTranslateFromConfiguration(configuration);
        var act = () => services.BuildServiceProvider().GetRequiredService<IAiTranslationProvider>();

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*No AI provider configured*");
    }

    [Fact]
    public void AddAutoTranslateWithPostgreSql_SetsCorrectOptions()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddScoped<IAiTranslationProvider, FakeAiProvider>();
        var connectionString = "Host=localhost;Database=test";
        var schema = "custom";

        // Act
        services.AddAutoTranslateWithPostgreSql(connectionString, schema);
        var provider = services.BuildServiceProvider();

        // Assert
        var translationService = provider.GetService<ITranslationService>();
        translationService.Should().NotBeNull();
        translationService.Should().BeOfType<EfTranslationService>();
    }

    [Fact]
    public void AddAutoTranslateWithSqlServer_SetsCorrectOptions()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddScoped<IAiTranslationProvider, FakeAiProvider>();
        var connectionString = "Server=localhost;Database=test";

        // Act
        services.AddAutoTranslateWithSqlServer(connectionString);
        var provider = services.BuildServiceProvider();

        // Assert
        var translationService = provider.GetService<ITranslationService>();
        translationService.Should().NotBeNull();
        translationService.Should().BeOfType<EfTranslationService>();
    }
}
