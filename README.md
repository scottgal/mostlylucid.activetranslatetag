# mostlylucid.activetranslatetag

**Active Translation Tag Helpers** - An open-source AI-powered translation system for ASP.NET Core from [mostlylucid.net](https://www.mostlylucid.net).

Auto-translation tag helpers for ASP.NET Core with HTMX integration and AI-powered translations. Automatically translates UI strings and user content with real-time updates via SignalR.

## Features

- **Multiple Storage Options**: PostgreSQL (with schema support), SQLite, or JSON file storage
- **Tag Helper Integration**: Automatically translate any HTML element or use manual translation keys
- **HTMX OOB Swaps**: Efficient language switching without page reloads
- **AI-Powered Translation**: Pluggable AI translation provider interface
- **Real-time Updates**: SignalR integration for live translation progress
- **UI Components**: Built-in language selector and status display tag helpers
- **Multi-level Caching**: Request-scoped, memory cache, and persistent storage
- **Deterministic IDs**: Hash-based element IDs for reliable HTMX targeting

## Installation

```bash
dotnet add package mostlylucid.activetranslatetag
```

## Quick Start (Configuration-Based)

The easiest way to get started is using the configuration-based setup. Add your settings to `appsettings.json` and call a single setup method.

**appsettings.json:**

```json
{
  "LlmTranslate": {
    "Storage": {
      "StorageType": "InMemory",
      "EnableMemoryCache": true,
      "MemoryCacheDurationMinutes": 60
    },
    "Ai": {
      "DefaultProvider": "easynmt-local",
      "Chunking": {
        "Enabled": false,
        "ChunkLength": 4000,
        "Overlap": 200
      },
      "EasyNmtProviders": [
        {
          "Name": "easynmt-local",
          "BaseUrl": "http://localhost:24080/"
        }
      ],
      "OllamaProviders": [
        {
          "Name": "ollama-local",
          "BaseUrl": "http://localhost:11434/",
          "Model": "llama3"
        }
      ]
    }
  }
}
```

**Program.cs:**

```csharp
// Add AutoTranslate from configuration
builder.Services.AddAutoTranslateFromConfiguration(builder.Configuration);

var app = builder.Build();

// Map the translation endpoints (controllers + SignalR hub)
app.MapLlmTranslateEndpoints();

app.Run();
```

This configuration:
- Uses EasyNMT at port 24080 as the default provider (fast, local, no API keys)
- Configures Ollama with llama3 as an alternative LLM provider
- Uses in-memory storage for development (switch to PostgreSQL/SQLite for production)
- Enables caching for better performance

## Minimal Configuration

If you want the absolute minimal setup without any configuration files, you can register services directly:

**Program.cs (minimal):**

```csharp
using mostlylucid.activetranslatetag.Extensions;
using mostlylucid.activetranslatetag.Services;
using mostlylucid.activetranslatetag.Services.Providers;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();

// Minimal setup: in-memory storage only (no AI provider)
builder.Services.AddAutoTranslate(options =>
{
    options.StorageType = TranslationStorageType.InMemory;
});

var app = builder.Build();

app.UseStaticFiles();
app.UseRouting();

// Map translation endpoints
app.MapLlmTranslateEndpoints();
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
```

**_ViewImports.cshtml:**

```cshtml
@addTagHelper *, mostlylucid.activetranslatetag
```

**Layout (minimal):**

```html
<!DOCTYPE html>
<html>
<head>
    <title>My App</title>
</head>
<body>
    <h1 auto-translate="true">Welcome</h1>
    @RenderBody()

    <translation-scripts />
</body>
</html>
```

That's it! Without an AI provider, translations will be stored but not automatically generated. You can manually add translations via the API or later add a provider.

**To add EasyNMT (recommended):**

```csharp
// Start EasyNMT server
// docker run -p 24080:80 easynmt/api:2.0.2-cpu

// Add to Program.cs after AddAutoTranslate
builder.Services.AddHttpClient<IAiTranslationProvider, EasyNmtTranslationProvider>(client =>
{
    client.BaseAddress = new Uri("http://localhost:24080/");
});
builder.Services.AddScoped<IAiTranslationProvider>(sp =>
{
    var http = sp.GetRequiredService<IHttpClientFactory>().CreateClient();
    http.BaseAddress = new Uri("http://localhost:24080/");
    var logger = sp.GetRequiredService<ILogger<EasyNmtTranslationProvider>>();
    return new EasyNmtTranslationProvider(http, logger, "http://localhost:24080/");
});
```

## Storage Configuration

### Option 1: PostgreSQL (Recommended for Production)

**With default schema (public):**

```csharp
builder.Services.AddAutoTranslateWithPostgreSql(
    builder.Configuration.GetConnectionString("DefaultConnection")
);
```

**With custom schema:**

```csharp
builder.Services.AddAutoTranslateWithPostgreSql(
    connectionString: builder.Configuration.GetConnectionString("DefaultConnection"),
    schema: "translations"  // Custom schema name
);
```

**Using configuration options:**

```csharp
builder.Services.AddAutoTranslate(options =>
{
    options.StorageType = TranslationStorageType.PostgreSql;
    options.ConnectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    options.PostgreSqlSchema = "translations"; // Optional, defaults to "public"
    options.EnableMemoryCache = true;
    options.MemoryCacheDurationMinutes = 60;
});
```

**Connection string example:**

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Database=myapp;Username=postgres;Password=secret"
  }
}
```

**Run migrations:**

```bash
dotnet ef migrations add InitialTranslation --context TranslationDbContext
dotnet ef database update --context TranslationDbContext
```

### Option 2: SQLite (Great for Development/Small Apps)

**Basic setup:**

```csharp
builder.Services.AddAutoTranslateWithSqlite(
    "Data Source=translations.db"
);
```

**With configuration options:**

```csharp
builder.Services.AddAutoTranslate(options =>
{
    options.StorageType = TranslationStorageType.Sqlite;
    options.ConnectionString = "Data Source=translations.db;Cache=Shared";
    options.EnableMemoryCache = true;
    options.MemoryCacheDurationMinutes = 120; // Longer cache for file-based DB
});
```

**Run migrations:**

```bash
dotnet ef migrations add InitialTranslation --context TranslationDbContext
dotnet ef database update --context TranslationDbContext
```

### Option 3: JSON File (Simple/Portable)

Perfect for:
- Static site generation
- Version control of translations
- Simple deployments
- Development/testing

```csharp
builder.Services.AddAutoTranslateWithJsonFile(
    filePath: "App_Data/translations.json",
    autoSave: true  // Auto-save changes immediately
);
```

**With configuration options:**

```csharp
builder.Services.AddAutoTranslate(options =>
{
    options.StorageType = TranslationStorageType.JsonFile;
    options.JsonFilePath = Path.Combine(builder.Environment.ContentRootPath, "translations.json");
    options.JsonAutoSave = true; // Save after each change
    options.EnableMemoryCache = true;
    options.MemoryCacheDurationMinutes = 30;
});
```

**JSON file structure:**

```json
{
  "strings": [
    {
      "id": 1,
      "key": "home.welcome",
      "defaultText": "Welcome to our site",
      "category": "Home",
      "context": "Main page greeting",
      "translations": [
        {
          "id": 1,
          "languageCode": "es",
          "translatedText": "Bienvenido a nuestro sitio",
          "source": 1,
          "aiModel": "gpt-4"
        },
        {
          "id": 2,
          "languageCode": "fr",
          "translatedText": "Bienvenue sur notre site",
          "source": 1
        }
      ]
    }
  ]
}
```

### Option 4: Existing DbContext

If you already have a DbContext, make it implement `ITranslationDbContext`:

```csharp
public class ApplicationDbContext : DbContext, ITranslationDbContext
{
    public DbSet<TranslationString> TranslationStrings => Set<TranslationString>();
    public DbSet<Translation> Translations => Set<Translation>();

    // ... your other DbSets
}
```

Then register:

```csharp
builder.Services.AddAutoTranslateWithExistingDbContext<ApplicationDbContext>();
```

## Built-in AI Translation Providers

The package ships with ready-to-use providers. Pick one, add minimal configuration, and register it for IAiTranslationProvider.

Supported built-ins:
- OpenAI (hosted)
- Azure OpenAI (Azure AI) Chat Completions
- Ollama (local LLM)
- LM Studio (local, OpenAI-compatible)
- EasyNMT (local machine translation)

General registration pattern (Program.cs):

```csharp
// Select ONE provider registration below
builder.Services.AddAutoTranslateWithSqlite("Data Source=translations.db");

var cfg = builder.Configuration;
var httpFactory = builder.Services.AddHttpClient(); // ensures IHttpClientFactory is available
```

Notes:
- For security, store secrets in User Secrets or environment variables, not in source control.
- Each example below shows appsettings.json plus DI registration using IHttpClientFactory.

### OpenAI (hosted)

appsettings.json:
```json
{
  "OpenAI": {
    "ApiKey": "${OPENAI_API_KEY}",
    "Model": "gpt-4o-mini"
  }
}
```

Note: Use a model suited for translation such as gpt-4o-mini, gpt-4o, or o3-mini.

Environment variables (alternative):
- OPENAI_API_KEY
- OPENAI_MODEL (optional)

Program.cs registration:
```csharp
builder.Services.AddHttpClient("OpenAI", c => c.BaseAddress = new Uri("https://api.openai.com/"));
builder.Services.AddScoped<IAiTranslationProvider>(sp =>
{
    var http = sp.GetRequiredService<IHttpClientFactory>().CreateClient("OpenAI");
    var logger = sp.GetRequiredService<ILogger<mostlylucid.activetranslatetag.Services.Providers.OpenAiTranslationProvider>>();
    var apiKey = cfg["OpenAI:ApiKey"] ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY")!;
    var model = cfg["OpenAI:Model"] ?? Environment.GetEnvironmentVariable("OPENAI_MODEL") ?? "gpt-4o-mini";
    return new mostlylucid.activetranslatetag.Services.Providers.OpenAiTranslationProvider(http, logger, apiKey, model);
});
```

### Azure OpenAI (Azure AI)

You need your resource endpoint, deployment name, and an API key. The provider defaults api-version to 2024-06-01; override if needed.

appsettings.json:
```json
{
  "AzureOpenAI": {
    "Endpoint": "https://your-resource.openai.azure.com",
    "ApiKey": "${AZURE_OPENAI_API_KEY}",
    "Deployment": "gpt-4o-mini",
    "ApiVersion": "2024-06-01"
  }
}
```

Environment variables (alternative):
- AZURE_OPENAI_ENDPOINT
- AZURE_OPENAI_API_KEY
- AZURE_OPENAI_DEPLOYMENT
- AZURE_OPENAI_API_VERSION (optional)

Program.cs registration:
```csharp
builder.Services.AddHttpClient("AzureOpenAI");
builder.Services.AddScoped<IAiTranslationProvider>(sp =>
{
    var http = sp.GetRequiredService<IHttpClientFactory>().CreateClient("AzureOpenAI");
    var logger = sp.GetRequiredService<ILogger<mostlylucid.activetranslatetag.Services.Providers.AzureAiTranslationProvider>>();
    var endpoint = cfg["AzureOpenAI:Endpoint"] ?? Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT")!;
    var apiKey = cfg["AzureOpenAI:ApiKey"] ?? Environment.GetEnvironmentVariable("AZURE_OPENAI_API_KEY")!;
    var deployment = cfg["AzureOpenAI:Deployment"] ?? Environment.GetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT")!;
    var apiVersion = cfg["AzureOpenAI:ApiVersion"] ?? Environment.GetEnvironmentVariable("AZURE_OPENAI_API_VERSION") ?? "2024-06-01";
    return new mostlylucid.activetranslatetag.Services.Providers.AzureAiTranslationProvider(http, logger, endpoint, apiKey, deployment, apiVersion);
});
```

### Ollama (local LLM) - **Recommended for LLM Translation**

Run Ollama locally for LLM-powered translation with context awareness. Supports the `description` parameter for better translation quality.

**Why Ollama:**
- Runs entirely on your hardware
- No API costs
- Privacy-focused
- Supports context via description parameter
- Access to many open-source models

**Setup:**
```bash
# Install Ollama (see https://ollama.ai)
# Pull the llama3 model
ollama pull llama3
```

**appsettings.json:**
```json
{
  "Ollama": {
    "BaseUrl": "http://localhost:11434/",
    "Model": "llama3"
  }
}
```

**Environment variables (alternative):**
- OLLAMA_BASE_URL (optional, defaults to http://localhost:11434/)
- OLLAMA_MODEL (optional, defaults to llama3)

**Program.cs registration:**
```csharp
builder.Services.AddHttpClient("Ollama", c => c.BaseAddress = new Uri(cfg["Ollama:BaseUrl"] ?? Environment.GetEnvironmentVariable("OLLAMA_BASE_URL") ?? "http://localhost:11434/"));
builder.Services.AddScoped<IAiTranslationProvider>(sp =>
{
    var http = sp.GetRequiredService<IHttpClientFactory>().CreateClient("Ollama");
    var logger = sp.GetRequiredService<ILogger<mostlylucid.activetranslatetag.Services.Providers.OllamaTranslationProvider>>();
    var model = cfg["Ollama:Model"] ?? Environment.GetEnvironmentVariable("OLLAMA_MODEL") ?? "llama3";
    var baseUrl = cfg["Ollama:BaseUrl"] ?? Environment.GetEnvironmentVariable("OLLAMA_BASE_URL");
    return new mostlylucid.activetranslatetag.Services.Providers.OllamaTranslationProvider(http, logger, model, baseUrl);
});
```

**Recommended models:**
- `llama3` - Good balance of speed and quality
- `llama3.1` - Improved performance
- `qwen2.5` - Excellent for multilingual translation

### LM Studio (local, OpenAI-compatible)

LM Studio exposes an OpenAI-like API (default http://localhost:1234/v1). Choose a local model you have loaded.

appsettings.json:
```json
{
  "LmStudio": {
    "BaseUrl": "http://localhost:1234/",
    "Model": "qwen2.5:latest"
  }
}
```

Environment variables (alternative):
- LMSTUDIO_BASE_URL (optional, defaults to http://localhost:1234/)
- LMSTUDIO_MODEL (optional, defaults to qwen2.5:latest)

Program.cs registration:
```csharp
builder.Services.AddHttpClient("LmStudio", c => c.BaseAddress = new Uri(cfg["LmStudio:BaseUrl"] ?? Environment.GetEnvironmentVariable("LMSTUDIO_BASE_URL") ?? "http://localhost:1234/"));
builder.Services.AddScoped<IAiTranslationProvider>(sp =>
{
    var http = sp.GetRequiredService<IHttpClientFactory>().CreateClient("LmStudio");
    var logger = sp.GetRequiredService<ILogger<mostlylucid.activetranslatetag.Services.Providers.LmStudioTranslationProvider>>();
    var model = cfg["LmStudio:Model"] ?? Environment.GetEnvironmentVariable("LMSTUDIO_MODEL") ?? "qwen2.5:latest";
    var baseUrl = cfg["LmStudio:BaseUrl"] ?? Environment.GetEnvironmentVariable("LMSTUDIO_BASE_URL");
    return new mostlylucid.activetranslatetag.Services.Providers.LmStudioTranslationProvider(http, logger, model, baseUrl);
});
```

### EasyNMT (local machine translation) - **Recommended Default**

Run EasyNMT locally for fast, privacy-focused machine translation without API keys. This is the recommended default provider for most applications.

**Why EasyNMT:**
- No API keys or cloud dependencies
- Fast translation (typically <1 second)
- Runs entirely on your infrastructure
- Supports many language pairs
- Free and open source

**Docker setup:**
```bash
# CPU version (lighter weight)
docker run -p 24080:80 easynmt/api:2.0.2-cpu

# GPU version (faster, requires NVIDIA GPU)
docker run --gpus all -p 24080:80 easynmt/api:2.0.2
```

**appsettings.json:**
```json
{
  "EasyNMT": {
    "BaseUrl": "http://localhost:24080/"
  }
}
```

**Environment variables (alternative):**
- EASYNMT_BASE_URL (optional, defaults to http://localhost:24080/)

**Program.cs registration:**
```csharp
builder.Services.AddHttpClient("EasyNMT");
builder.Services.AddScoped<IAiTranslationProvider>(sp =>
{
    var http = sp.GetRequiredService<IHttpClientFactory>().CreateClient("EasyNMT");
    var logger = sp.GetRequiredService<ILogger<mostlylucid.activetranslatetag.Services.Providers.EasyNmtTranslationProvider>>();
    var baseUrl = cfg["EasyNMT:BaseUrl"] ?? Environment.GetEnvironmentVariable("EASYNMT_BASE_URL") ?? "http://localhost:24080/";
    return new mostlylucid.activetranslatetag.Services.Providers.EasyNmtTranslationProvider(http, logger, baseUrl);
});
```

**Note:** EasyNMT is a machine translation provider and does not support the `description` parameter (used for providing context to LLM providers).

Switching providers: register only one IAiTranslationProvider at a time, or use named registrations and a small adapter to pick one via config.

Provider behavior notes:
- All providers support single and batch translation; if batch JSON parsing fails, they fall back to per-item translation.
- Prompts preserve placeholders like {0} and {name}, and HTML tags; still validate outputs for your UI.

## Application Configuration

```csharp
// In Program.cs, configure services
builder.Services.AddAutoTranslateWithSqlite("Data Source=translations.db");
builder.Services.AddScoped<IAiTranslationProvider, YourAiProvider>();

// Map the SignalR hub
app.MapAutoTranslateHub(); // Default: /hubs/translation

// Map controllers (if not already done)
app.MapControllers();
```

## View Configuration

Add to `_ViewImports.cshtml`:

```cshtml
@addTagHelper *, mostlylucid.activetranslatetag
```

## Usage

### 1. Auto-Translate Tag Helper

Automatically translates any HTML element:

```html
<h1 auto-translate="true">Welcome to My App</h1>
<p auto-translate="true" translation-category="home">
    This text will be automatically translated
</p>
<button auto-translate="true" translation-category="nav">
    Click Me
</button>
```

**With description parameter (LLM providers only):**

The `translation-description` attribute provides context to LLM providers (like Ollama, OpenAI, Azure) to improve translation quality. Machine translation providers (EasyNMT, CTranslate2) ignore this parameter.

```html
<h1 auto-translate="true"
    translation-description="Main headline for tech startup homepage">
    Revolutionize Your Workflow
</h1>

<p auto-translate="true"
   translation-category="marketing"
   translation-description="Value proposition for B2B customers">
    Streamline operations and boost productivity with our AI-powered platform.
</p>

<button auto-translate="true"
        translation-description="Call-to-action button for free trial signup">
    Start Free Trial
</button>
```

### 2. Manual Translation Tag Helper

Use explicit translation keys:

```html
<t key="home.welcome">Welcome</t>
<t key="nav.home" category="navigation">Home</t>
<t key="greeting" lang="es">Hello</t>
```

**With description parameter (LLM providers only):**

```html
<t key="home.hero"
   description="Marketing slogan for tech startup targeting developers">
    Build faster, ship smarter
</t>

<t key="pricing.cta"
   description="Pricing page call-to-action for enterprise customers">
    Contact Sales
</t>

<t key="nav.features"
   category="navigation"
   description="Navigation link to product features page">
    Features
</t>
```

**When to use description:**
- Marketing copy that needs tone/style matching
- Domain-specific terminology (legal, medical, technical)
- Culturally-sensitive content
- Ambiguous phrases that could be translated multiple ways
- Brand voice consistency across languages

### 3. Language Selector

Add a language selector dropdown:

```html
<!-- Simple dropdown -->
<language-selector style="dropdown" />

<!-- With custom languages -->
<language-selector
    style="dropdown"
    languages="en,es,fr,de,ja"
    show-names="true"
    show-codes="true" />

<!-- Button style -->
<language-selector style="buttons" class="my-custom-class" />

<!-- Select box -->
<language-selector style="select" />

<!-- Flag icons (requires flag-icon-css) -->
<language-selector style="flags" show-names="true" />
```

**Selector styles:**
- `dropdown` - Bootstrap dropdown (default)
- `buttons` - Button group
- `select` - HTML select element
- `flags` - Flag icons with names

### 4. Translation Status Display

Show current language and translation progress:

```html
<!-- Top-right corner (default) -->
<translation-status />

<!-- With detailed progress -->
<translation-status position="top-right" show-details="true" />

<!-- Different positions -->
<translation-status position="bottom-left" />
<translation-status position="inline" />
```

**Positions:**
- `top-right` (default)
- `top-left`
- `bottom-right`
- `bottom-left`
- `inline`

### 5. Include Scripts

Add to your layout (before closing `</body>` tag):

```html
<!-- Basic (includes SignalR from CDN) -->
<translation-scripts />

<!-- With configuration -->
<translation-scripts
    signalr-hub="/hubs/translation"
    include-signalr="true"
    debug="false"
    enable-notifications="true" />

<!-- Without SignalR (no real-time updates) -->
<translation-scripts include-signalr="false" />

<!-- Debug mode -->
<translation-scripts debug="true" />
```

## Complete Layout Example

```html
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="utf-8" />
    <title>My App</title>
    <link rel="stylesheet" href="~/css/site.css" />
</head>
<body>
    <header>
        <nav>
            <t key="nav.home">Home</t>
            <t key="nav.about">About</t>
            <language-selector style="dropdown" languages="en,es,fr,de,ja" />
        </nav>
    </header>

    <main>
        @RenderBody()
    </main>

    <footer>
        <p auto-translate="true" translation-category="footer">
            © 2025 My Company. All rights reserved.
        </p>
    </footer>

    <!-- Translation status indicator -->
    <translation-status position="top-right" show-details="true" />

    <!-- Translation scripts (must be at end of body) -->
    <translation-scripts
        signalr-hub="/hubs/translation"
        debug="@(Environment.IsDevelopment())" />
</body>
</html>
```

## Configuration Matrix

| Storage Type | Best For | Pros | Cons |
|---|---|---|---|
| **PostgreSQL** | Production, high traffic | Best performance, ACID, scalability, schema isolation | Requires PostgreSQL server |
| **SQLite** | Small apps, development | Simple setup, file-based, portable | Not for high concurrency |
| **JSON File** | Static sites, dev, version control | Human-readable, portable, no DB required | Slower for large datasets |

## Complete Configuration Reference

### Configuration-Based Setup (appsettings.json)

The `AddAutoTranslateFromConfiguration` method reads settings from the `LlmTranslate` section of your configuration.

**Complete appsettings.json example with all options:**

```json
{
  "LlmTranslate": {
    "Storage": {
      "StorageType": "PostgreSql",
      "ConnectionString": "Host=localhost;Database=myapp;Username=postgres;Password=secret",
      "PostgreSqlSchema": "translations",
      "JsonFilePath": "App_Data/translations.json",
      "JsonAutoSave": true,
      "EnableMemoryCache": true,
      "MemoryCacheDurationMinutes": 60
    },
    "Ai": {
      "DefaultProvider": "easynmt-local",
      "Chunking": {
        "Enabled": false,
        "ChunkLength": 4000,
        "Overlap": 200
      },
      "EasyNmtProviders": [
        {
          "Name": "easynmt-local",
          "BaseUrl": "http://localhost:24080/"
        },
        {
          "Name": "easynmt-remote",
          "BaseUrl": "https://translate.example.com/"
        }
      ],
      "CTranslate2Providers": [
        {
          "Name": "ctranslate2-local",
          "BaseUrl": "http://localhost:5000/"
        }
      ],
      "OllamaProviders": [
        {
          "Name": "ollama-local",
          "BaseUrl": "http://localhost:11434/",
          "Model": "llama3"
        },
        {
          "Name": "ollama-qwen",
          "BaseUrl": "http://localhost:11434/",
          "Model": "qwen2.5"
        }
      ],
      "OpenAiProviders": [
        {
          "Name": "openai-gpt4o",
          "ApiKey": "${OPENAI_API_KEY}",
          "Model": "gpt-4o-mini"
        }
      ],
      "AzureAiProviders": [
        {
          "Name": "azure-gpt4",
          "Endpoint": "https://your-resource.openai.azure.com",
          "ApiKey": "${AZURE_OPENAI_API_KEY}",
          "Deployment": "gpt-4o-mini",
          "ApiVersion": "2024-06-01"
        }
      ],
      "LmStudioProviders": [
        {
          "Name": "lmstudio-local",
          "BaseUrl": "http://localhost:1234/",
          "Model": "qwen2.5:latest"
        }
      ]
    }
  }
}
```

### Configuration Options Reference

#### Storage Section

| Option | Type | Default | Description |
|--------|------|---------|-------------|
| `StorageType` | string | `InMemory` | Storage backend: `InMemory`, `PostgreSql`, `Sqlite`, `JsonFile` |
| `ConnectionString` | string | null | Database connection string (required for PostgreSQL/SQLite) |
| `PostgreSqlSchema` | string | `"public"` | PostgreSQL schema name for translation tables |
| `JsonFilePath` | string | null | Path to JSON file (required for JsonFile storage) |
| `JsonAutoSave` | bool | `true` | Auto-save changes to JSON file immediately |
| `EnableMemoryCache` | bool | `true` | Enable in-memory caching for faster lookups |
| `MemoryCacheDurationMinutes` | int | `60` | How long to cache translations in memory |

#### AI Section

| Option | Type | Default | Description |
|--------|------|---------|-------------|
| `DefaultProvider` | string | null | Name of the default provider to use (must match a provider Name) |

#### Chunking Settings

| Option | Type | Default | Description |
|--------|------|---------|-------------|
| `Enabled` | bool | `false` | Enable text chunking for large documents |
| `ChunkLength` | int | `4000` | Maximum characters per chunk |
| `Overlap` | int | `200` | Character overlap between chunks |

#### Provider Configurations

Each provider type has an array of provider configurations. All providers share:

| Option | Type | Required | Description |
|--------|------|----------|-------------|
| `Name` | string | Yes | Unique identifier for this provider instance |

**EasyNmtProviders / CTranslate2Providers:**

| Option | Type | Required | Description |
|--------|------|----------|-------------|
| `BaseUrl` | string | Yes | Base URL of the translation server |

**OllamaProviders:**

| Option | Type | Required | Description |
|--------|------|----------|-------------|
| `BaseUrl` | string | No | Ollama server URL (default: http://localhost:11434/) |
| `Model` | string | Yes | Model name (e.g., llama3, qwen2.5) |

**OpenAiProviders:**

| Option | Type | Required | Description |
|--------|------|----------|-------------|
| `ApiKey` | string | Yes | OpenAI API key |
| `Model` | string | Yes | Model name (e.g., gpt-4o-mini) |

**AzureAiProviders:**

| Option | Type | Required | Description |
|--------|------|----------|-------------|
| `Endpoint` | string | Yes | Azure OpenAI resource endpoint |
| `ApiKey` | string | Yes | Azure OpenAI API key |
| `Deployment` | string | Yes | Deployment name |
| `ApiVersion` | string | No | API version (default: 2024-06-01) |

**LmStudioProviders:**

| Option | Type | Required | Description |
|--------|------|----------|-------------|
| `BaseUrl` | string | No | LM Studio server URL (default: http://localhost:1234/) |
| `Model` | string | Yes | Model name loaded in LM Studio |

### Using Environment Variables

Sensitive values like API keys should be stored in environment variables or user secrets:

```json
{
  "LlmTranslate": {
    "Ai": {
      "OpenAiProviders": [
        {
          "Name": "openai",
          "ApiKey": "${OPENAI_API_KEY}",
          "Model": "gpt-4o-mini"
        }
      ]
    }
  }
}
```

Then set the environment variable:
```bash
export OPENAI_API_KEY="sk-..."
```

Or use .NET User Secrets:
```bash
dotnet user-secrets set "OPENAI_API_KEY" "sk-..."
```

## Advanced Configuration

### Custom Cache Settings

```csharp
builder.Services.AddAutoTranslate(options =>
{
    options.StorageType = TranslationStorageType.PostgreSql;
    options.ConnectionString = "...";
    options.EnableMemoryCache = true;
    options.MemoryCacheDurationMinutes = 120; // 2 hours
});
```

### Multiple Languages Configuration

```csharp
// In appsettings.json
{
  "Translation": {
    "SupportedLanguages": ["en", "es", "fr", "de", "ja", "zh", "ar"],
    "DefaultLanguage": "en"
  }
}
```

### Environment-Specific Storage

```csharp
if (builder.Environment.IsDevelopment())
{
    // Use JSON file in development
    builder.Services.AddAutoTranslateWithJsonFile("translations.json");
}
else
{
    // Use PostgreSQL in production
    builder.Services.AddAutoTranslateWithPostgreSql(
        builder.Configuration.GetConnectionString("Production"),
        schema: "translations"
    );
}
```

## JavaScript API

The translation system exposes a global JavaScript API:

```javascript
// Switch language
window.setLanguage('es');

// Or use the manager directly
window.translationManager.switchLanguage('fr');

// Get current language
const lang = window.translationManager.getCurrentLanguage();

// Collect all translation keys on page
const keys = window.translationManager.collectTranslationKeys();
```

## Troubleshooting

### Translations not appearing

1. Check browser console for errors
2. Verify SignalR connection (Network tab → WS)
3. Check `preferred-language` cookie is set
4. Enable debug mode: `<translation-scripts debug="true" />`

### Database migrations failing

```bash
# For PostgreSQL with custom schema
dotnet ef migrations add Initial --context TranslationDbContext -- --schema translations

# For SQLite
dotnet ef migrations add Initial --context TranslationDbContext
```

### SignalR not connecting

1. Ensure `app.MapAutoTranslateHub()` is called
2. Check hub path matches: `<translation-scripts signalr-hub="/hubs/translation" />`
3. Verify SignalR CDN is accessible

### EasyNMT / mostlylucid-nmt Issues

**Translations returning unchanged text:**

1. Verify server is running:
```bash
curl http://localhost:24080/translate \
  -H "Content-Type: application/json" \
  -d '{"text":"Hello","target_lang":"es","source_lang":""}'
```

Expected response format:
```json
{
  "translated": ["Hola"]
}
```

2. Check server logs for errors
3. Ensure `source_lang` is empty string (not null) for auto-detection
4. Verify language codes are ISO 639-1 (e.g., en, es, fr, de)

**Docker container issues:**

```bash
# Check if container is running
docker ps

# View container logs
docker logs <container-id>

# Restart container
docker restart <container-id>

# Run with explicit port mapping
docker run -p 24080:80 easynmt/api:2.0.2-cpu
```

**Response format compatibility:**

The mostlylucid-nmt server uses `"translated": [...]` (array format). If using a different EasyNMT server that returns `"translation": "..."` (string format), the provider will auto-detect and handle both formats.

### Ollama Issues

**Model not found:**

```bash
# List available models
ollama list

# Pull the model
ollama pull llama3
```

**Connection refused:**

```bash
# Check Ollama is running
ollama serve

# Or check the service status (Linux)
systemctl status ollama
```

**Slow translation:**

- Use smaller models (llama3 instead of llama3.1:70b)
- Enable GPU acceleration if available
- Reduce concurrent requests

## Performance Tips

1. **Use PostgreSQL for production** - best performance and concurrency
2. **Enable memory caching** - reduces database queries
3. **Implement batch translation** - faster than individual translations
4. **Use JSON file for static content** - version control friendly
5. **Set appropriate cache duration** - balance freshness vs performance

## Requirements

- .NET 9.0+
- ASP.NET Core
- One of: PostgreSQL, SQLite, or file system access
- SignalR (included in ASP.NET Core)

## License

Unlicense

## Author

mostlylucid.net - [https://www.mostlylucid.net](https://www.mostlylucid.net)

## Contributing

Contributions welcome! Please open an issue or PR at [https://github.com/mostlylucid/mostlylucid.activetranslatetag](https://github.com/mostlylucid/mostlylucid.activetranslatetag)

## Support

- Documentation: This README
- GitHub Issues: [Report issues](https://github.com/mostlylucid/mostlylucid.activetranslatetag/issues)
- Website: [mostlylucid.net](https://www.mostlylucid.net)
