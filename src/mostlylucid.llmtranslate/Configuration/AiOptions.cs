namespace mostlylucid.llmtranslate.Configuration;

/// <summary>
/// AI provider configuration bound from configuration section "LlmTranslate:Ai".
/// Supports multiple Ollama providers and a default selection. Back-compat fields for EasyNMT/OpenAI remain optional.
/// </summary>
public class AiOptions
{
    /// <summary>
    /// Name of the default provider to use for translations. Must match one of the configured providers' Name.
    /// </summary>
    public string? DefaultProvider { get; set; }

    /// <summary>
    /// Chunking options (optional). If enabled, long inputs will be split into chunks before translation.
    /// </summary>
    public ChunkingOptions Chunking { get; set; } = new();

    /// <summary>
    /// Collection of Ollama providers.
    /// </summary>
    public OllamaProviderOptions[] OllamaProviders { get; set; } = System.Array.Empty<OllamaProviderOptions>();

    /// <summary>
    /// Collection of EasyNMT providers (back-compat). Optional.
    /// </summary>
    public EasyNmtProviderOptions[] EasyNmtProviders { get; set; } = System.Array.Empty<EasyNmtProviderOptions>();

    /// <summary>
    /// Collection of OpenAI providers (back-compat). Optional.
    /// </summary>
    public OpenAiProviderOptions[] OpenAiProviders { get; set; } = System.Array.Empty<OpenAiProviderOptions>();
}

/// <summary>
/// Chunking configuration to split long texts into manageable pieces for providers with token/length limits.
/// </summary>
public class ChunkingOptions
{
    /// <summary>
    /// Enable chunking decorator.
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// Maximum length of each chunk (characters). Defaults to 800 if not specified.
    /// </summary>
    public int ChunkLength { get; set; } = 800;

    /// <summary>
    /// Overlap (characters) between consecutive chunks to improve continuity. Defaults to 0.
    /// </summary>
    public int Overlap { get; set; } = 0;
}

/// <summary>
/// Options for a single EasyNMT provider instance.
/// </summary>
public class EasyNmtProviderOptions
{
    /// <summary>
    /// Logical name of this provider instance (used by DefaultProvider selection).
    /// </summary>
    public string Name { get; set; } = "default";

    /// <summary>
    /// Base URL of the EasyNMT server, e.g., http://localhost:8080/
    /// </summary>
    public string BaseUrl { get; set; } = "http://localhost:24080/";
}

/// <summary>
/// Options for a single OpenAI provider instance.
/// </summary>
public class OllamaProviderOptions
{
    /// <summary>
    /// Logical name of this provider instance (used by DefaultProvider selection).
    /// </summary>
    public string Name { get; set; } = "ollama";

    /// <summary>
    /// Base URL of the Ollama server, e.g., http://localhost:11434/
    /// </summary>
    public string BaseUrl { get; set; } = "http://localhost:11434/";

    /// <summary>
    /// Model to use, e.g., llama3.1
    /// </summary>
    public string Model { get; set; } = "llama3.1";
}

/// <summary>
/// Options for a single OpenAI provider instance.
/// </summary>
public class OpenAiProviderOptions
{
    /// <summary>
    /// Logical name of this provider instance (used by DefaultProvider selection).
    /// </summary>
    public string Name { get; set; } = "openai";

    /// <summary>
    /// API key for OpenAI-compatible endpoint.
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// Model id, e.g., gpt-4o-mini.
    /// </summary>
    public string Model { get; set; } = "gpt-4o-mini";

    /// <summary>
    /// Optional base URL override for OpenAI-compatible APIs. If empty, uses api.openai.com.
    /// </summary>
    public string? BaseUrl { get; set; }
}
