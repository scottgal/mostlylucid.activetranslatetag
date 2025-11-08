using mostlylucid.llmtranslate.Hubs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using mostlylucid.llmtranslate.Configuration;
using mostlylucid.llmtranslate.Data;
using mostlylucid.llmtranslate.Helpers;
using mostlylucid.llmtranslate.Services;
using mostlylucid.llmtranslate.Storage;

namespace mostlylucid.llmtranslate.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds auto-translate services using configuration options
    /// This is the recommended method for most scenarios
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <param name="configureOptions">Action to configure storage options</param>
    /// <returns>Service collection for chaining</returns>
    public static IServiceCollection AddAutoTranslate(
        this IServiceCollection services,
        Action<TranslationStorageOptions> configureOptions)
    {
        var options = new TranslationStorageOptions();
        configureOptions(options);

        return services.AddAutoTranslate(options);
    }

    /// <summary>
    /// Adds auto-translate services using configuration options instance
    /// </summary>
    public static IServiceCollection AddAutoTranslate(
        this IServiceCollection services,
        TranslationStorageOptions options)
    {
        // Register common services
        services.AddScoped<RequestTranslationCache>();
        services.AddScoped<TranslationHelper>();

        services.AddHttpContextAccessor();
        services.AddMemoryCache();
        services.AddSignalR();
        services.AddControllers();

        // Configure storage based on type
        switch (options.StorageType)
        {
            case TranslationStorageType.PostgreSql:
                if (string.IsNullOrEmpty(options.ConnectionString))
                    throw new ArgumentException("ConnectionString is required for PostgreSQL storage");

                services.AddDbContext<TranslationDbContext>(dbOptions =>
                {
                    dbOptions.UseNpgsql(options.ConnectionString);
                }, ServiceLifetime.Scoped);

                services.AddScoped<ITranslationDbContext>(sp =>
                {
                    var dbContextOptions = sp.GetRequiredService<DbContextOptions<TranslationDbContext>>();
                    return new TranslationDbContext(dbContextOptions, options.PostgreSqlSchema);
                });

                services.AddScoped<ITranslationService>(sp =>
                    new EfTranslationService(
                        sp.GetRequiredService<ITranslationDbContext>(),
                        sp.GetRequiredService<IAiTranslationProvider>(),
                        sp.GetRequiredService<IMemoryCache>(),
                        sp.GetRequiredService<RequestTranslationCache>(),
                        sp.GetRequiredService<ILogger<EfTranslationService>>(),
                        options.EnableMemoryCache,
                        options.MemoryCacheDurationMinutes));
                break;

            case TranslationStorageType.Sqlite:
                if (string.IsNullOrEmpty(options.ConnectionString))
                    throw new ArgumentException("ConnectionString is required for SQLite storage");

                services.AddDbContext<TranslationDbContext>(dbOptions =>
                {
                    dbOptions.UseSqlite(options.ConnectionString);
                }, ServiceLifetime.Scoped);

                services.AddScoped<ITranslationDbContext>(sp =>
                    sp.GetRequiredService<TranslationDbContext>());

                services.AddScoped<ITranslationService>(sp =>
                    new EfTranslationService(
                        sp.GetRequiredService<ITranslationDbContext>(),
                        sp.GetRequiredService<IAiTranslationProvider>(),
                        sp.GetRequiredService<IMemoryCache>(),
                        sp.GetRequiredService<RequestTranslationCache>(),
                        sp.GetRequiredService<ILogger<EfTranslationService>>(),
                        options.EnableMemoryCache,
                        options.MemoryCacheDurationMinutes));
                break;

            case TranslationStorageType.SqlServer:
                if (string.IsNullOrEmpty(options.ConnectionString))
                    throw new ArgumentException("ConnectionString is required for SqlServer storage");

                services.AddDbContext<TranslationDbContext>(dbOptions =>
                {
                    dbOptions.UseSqlServer(options.ConnectionString);
                }, ServiceLifetime.Scoped);

                services.AddScoped<ITranslationDbContext>(sp =>
                    sp.GetRequiredService<TranslationDbContext>());

                services.AddScoped<ITranslationService>(sp =>
                    new EfTranslationService(
                        sp.GetRequiredService<ITranslationDbContext>(),
                        sp.GetRequiredService<IAiTranslationProvider>(),
                        sp.GetRequiredService<IMemoryCache>(),
                        sp.GetRequiredService<RequestTranslationCache>(),
                        sp.GetRequiredService<ILogger<EfTranslationService>>(),
                        options.EnableMemoryCache,
                        options.MemoryCacheDurationMinutes));
                break;

            case TranslationStorageType.InMemory:
                services.AddSingleton<Services.InMemory.InMemoryStore>();
                services.AddScoped<ITranslationService>(sp =>
                    new Services.InMemory.InMemoryTranslationService(
                        sp.GetRequiredService<Services.InMemory.InMemoryStore>(),
                        sp.GetRequiredService<IAiTranslationProvider>(),
                        sp.GetRequiredService<IMemoryCache>(),
                        sp.GetRequiredService<RequestTranslationCache>(),
                        sp.GetRequiredService<ILogger<Services.InMemory.InMemoryTranslationService>>(),
                        options.EnableMemoryCache,
                        options.MemoryCacheDurationMinutes));
                break;

            case TranslationStorageType.JsonFile:
                if (string.IsNullOrEmpty(options.JsonFilePath))
                    throw new ArgumentException("JsonFilePath is required for JSON file storage");

                services.AddSingleton(sp =>
                    new JsonFileTranslationStore(
                        options.JsonFilePath,
                        options.JsonAutoSave,
                        sp.GetRequiredService<ILogger<JsonFileTranslationStore>>()));

                services.AddScoped<ITranslationService>(sp =>
                    new JsonFileTranslationService(
                        sp.GetRequiredService<JsonFileTranslationStore>(),
                        sp.GetRequiredService<IAiTranslationProvider>(),
                        sp.GetRequiredService<IMemoryCache>(),
                        sp.GetRequiredService<RequestTranslationCache>(),
                        sp.GetRequiredService<ILogger<JsonFileTranslationService>>(),
                        options.EnableMemoryCache,
                        options.MemoryCacheDurationMinutes));
                break;

            default:
                throw new ArgumentException($"Unsupported storage type: {options.StorageType}");
        }

        // Register language switch service depending on provider kind
        if (options.StorageType == TranslationStorageType.JsonFile || options.StorageType == TranslationStorageType.InMemory)
        {
            services.AddScoped<IPageLanguageSwitchService, JsonPageLanguageSwitchService>();
        }
        else
        {
            services.AddScoped<IPageLanguageSwitchService, PageLanguageSwitchService>();
        }

        return services;
    }

    /// <summary>
    /// Adds auto-translate services with PostgreSQL database
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <param name="connectionString">PostgreSQL connection string</param>
    /// <param name="schema">PostgreSQL schema name (default: "public")</param>
    /// <returns>Service collection for chaining</returns>
    public static IServiceCollection AddAutoTranslateWithPostgreSql(
        this IServiceCollection services,
        string connectionString,
        string schema = "public")
    {
        return services.AddAutoTranslate(options =>
        {
            options.StorageType = TranslationStorageType.PostgreSql;
            options.ConnectionString = connectionString;
            options.PostgreSqlSchema = schema;
        });
    }

    /// <summary>
    /// Adds auto-translate services with SQLite database
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <param name="connectionString">SQLite connection string (e.g., "Data Source=translations.db")</param>
    /// <returns>Service collection for chaining</returns>
    public static IServiceCollection AddAutoTranslateWithSqlite(
        this IServiceCollection services,
        string connectionString)
    {
        return services.AddAutoTranslate(options =>
        {
            options.StorageType = TranslationStorageType.Sqlite;
            options.ConnectionString = connectionString;
        });
    }

    /// <summary>
    /// Adds auto-translate services with JSON file storage
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <param name="filePath">Path to JSON file</param>
    /// <param name="autoSave">Whether to auto-save changes immediately (default: true)</param>
    /// <returns>Service collection for chaining</returns>
    public static IServiceCollection AddAutoTranslateWithJsonFile(
        this IServiceCollection services,
        string filePath,
        bool autoSave = true)
    {
        return services.AddAutoTranslate(options =>
        {
            options.StorageType = TranslationStorageType.JsonFile;
            options.JsonFilePath = filePath;
            options.JsonAutoSave = autoSave;
        });
    }

    /// <summary>
    /// Adds auto-translate services with SQL Server database
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <param name="connectionString">SQL Server connection string</param>
    public static IServiceCollection AddAutoTranslateWithSqlServer(
        this IServiceCollection services,
        string connectionString)
    {
        return services.AddAutoTranslate(options =>
        {
            options.StorageType = TranslationStorageType.SqlServer;
            options.ConnectionString = connectionString;
        });
    }

    /// <summary>
    /// Adds auto-translate services with in-memory volatile storage
    /// </summary>
    public static IServiceCollection AddAutoTranslateInMemory(
        this IServiceCollection services)
    {
        return services.AddAutoTranslate(options =>
        {
            options.StorageType = TranslationStorageType.InMemory;
        });
    }

    /// <summary>
    /// Adds auto-translate by binding options from configuration section "LlmTranslate"
    /// </summary>
    public static IServiceCollection AddAutoTranslateFromConfiguration(
        this IServiceCollection services,
        IConfiguration configuration,
        string sectionName = "LlmTranslate")
    {
        var options = new mostlylucid.llmtranslate.Configuration.LlmTranslateOptions();
        configuration.GetSection(sectionName).Bind(options);

        // Register HttpClient factory (for providers)
        services.AddHttpClient();

        // Register IAiTranslationProvider from configuration (supports multiple providers, choose default)
        services.AddScoped<IAiTranslationProvider>(sp =>
        {
            var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
            var httpFactory = sp.GetRequiredService<System.Net.Http.IHttpClientFactory>();
            var ai = options.Ai ?? new mostlylucid.llmtranslate.Configuration.AiOptions();
            var defaultName = ai.DefaultProvider;

            // Try resolve default from Ollama list first
            mostlylucid.llmtranslate.Services.IAiTranslationProvider? selected = null;

            if (ai.OllamaProviders != null && ai.OllamaProviders.Length > 0)
            {
                var pick = ai.OllamaProviders.FirstOrDefault(p => !string.IsNullOrWhiteSpace(defaultName) && string.Equals(p.Name, defaultName, StringComparison.OrdinalIgnoreCase))
                           ?? (ai.OllamaProviders.Length == 1 ? ai.OllamaProviders[0] : null);
                if (pick != null)
                {
                    var http = httpFactory.CreateClient($"ollama:{pick.Name}");
                    if (!string.IsNullOrWhiteSpace(pick.BaseUrl))
                    {
                        http.BaseAddress = new Uri(pick.BaseUrl);
                    }
                    var provLogger = loggerFactory.CreateLogger<mostlylucid.llmtranslate.Services.Providers.OllamaTranslationProvider>();
                    selected = new mostlylucid.llmtranslate.Services.Providers.OllamaTranslationProvider(http, provLogger, pick.BaseUrl, pick.Model);
                }
            }

            // Back-compat: If not selected, try EasyNMT list
            if (selected == null && ai.EasyNmtProviders != null && ai.EasyNmtProviders.Length > 0)
            {
                var pick = ai.EasyNmtProviders.FirstOrDefault(p => !string.IsNullOrWhiteSpace(defaultName) && string.Equals(p.Name, defaultName, StringComparison.OrdinalIgnoreCase))
                           ?? (ai.EasyNmtProviders.Length == 1 ? ai.EasyNmtProviders[0] : null);
                if (pick != null)
                {
                    var http = httpFactory.CreateClient($"easynmt:{pick.Name}");
                    if (!string.IsNullOrWhiteSpace(pick.BaseUrl))
                    {
                        http.BaseAddress = new Uri(pick.BaseUrl);
                    }
                    var provLogger = loggerFactory.CreateLogger<mostlylucid.llmtranslate.Services.Providers.EasyNmtTranslationProvider>();
                    selected = new mostlylucid.llmtranslate.Services.Providers.EasyNmtTranslationProvider(http, provLogger, pick.BaseUrl);
                }
            }

            // Back-compat: If not selected, try OpenAI list
            if (selected == null && ai.OpenAiProviders != null && ai.OpenAiProviders.Length > 0)
            {
                var pick = ai.OpenAiProviders.FirstOrDefault(p => !string.IsNullOrWhiteSpace(defaultName) && string.Equals(p.Name, defaultName, StringComparison.OrdinalIgnoreCase))
                           ?? (ai.OpenAiProviders.Length == 1 ? ai.OpenAiProviders[0] : null);
                if (pick != null)
                {
                    var http = httpFactory.CreateClient($"openai:{pick.Name}");
                    if (!string.IsNullOrWhiteSpace(pick.BaseUrl))
                    {
                        http.BaseAddress = new Uri(pick.BaseUrl!);
                    }
                    var provLogger = loggerFactory.CreateLogger<mostlylucid.llmtranslate.Services.Providers.OpenAiTranslationProvider>();
                    selected = new mostlylucid.llmtranslate.Services.Providers.OpenAiTranslationProvider(http, provLogger, pick.ApiKey ?? string.Empty, pick.Model);
                }
            }

            // If still not selected, throw to make misconfiguration visible in early stage
            if (selected == null)
            {
                throw new InvalidOperationException("No AI provider configured. Please configure LlmTranslate:Ai with at least one provider and set DefaultProvider.");
            }

            // Apply chunking decorator if enabled
            var chunk = ai.Chunking ?? new mostlylucid.llmtranslate.Configuration.ChunkingOptions();
            if (chunk.Enabled)
            {
                var chunkLogger = loggerFactory.CreateLogger<mostlylucid.llmtranslate.Services.Providers.ChunkingAiTranslationProvider>();
                selected = new mostlylucid.llmtranslate.Services.Providers.ChunkingAiTranslationProvider(selected, chunk.ChunkLength, chunk.Overlap, chunkLogger);
            }

            return selected;
        });

        // Finally register storage and services
        return services.AddAutoTranslate(options.Storage);
    }

    /// <summary>
    /// Adds auto-translate services using an existing DbContext that implements ITranslationDbContext
    /// </summary>
    /// <typeparam name="TContext">Your DbContext type that implements ITranslationDbContext</typeparam>
    /// <param name="services">Service collection</param>
    /// <returns>Service collection for chaining</returns>
    public static IServiceCollection AddAutoTranslateWithExistingDbContext<TContext>(
        this IServiceCollection services)
        where TContext : DbContext, ITranslationDbContext
    {
        // Register the existing DbContext as ITranslationDbContext
        services.AddScoped<ITranslationDbContext>(sp => sp.GetRequiredService<TContext>());

        // Add auto-translate services (without registering TranslationDbContext)
        services.AddScoped<RequestTranslationCache>();
        services.AddScoped<ITranslationService>(sp =>
            new EfTranslationService(
                sp.GetRequiredService<ITranslationDbContext>(),
                sp.GetRequiredService<IAiTranslationProvider>(),
                sp.GetRequiredService<IMemoryCache>(),
                sp.GetRequiredService<RequestTranslationCache>(),
                sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<EfTranslationService>>()));
        services.AddScoped<IPageLanguageSwitchService, PageLanguageSwitchService>();
        services.AddScoped<TranslationHelper>();

        services.AddHttpContextAccessor();
        services.AddMemoryCache();
        services.AddSignalR();
        services.AddControllers();

        return services;
    }
}
