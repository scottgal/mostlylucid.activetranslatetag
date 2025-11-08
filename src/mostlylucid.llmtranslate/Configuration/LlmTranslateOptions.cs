namespace mostlylucid.llmtranslate.Configuration;

/// <summary>
/// Top-level options for mostlylucid.llmtranslate package.
/// Bind from configuration section "LlmTranslate".
/// </summary>
public class LlmTranslateOptions
{
    /// <summary>
    /// Storage options for translations
    /// </summary>
    public TranslationStorageOptions Storage { get; set; } = new();

    /// <summary>
    /// AI provider options (provider selection, chunking, per-provider settings)
    /// </summary>
    public AiOptions Ai { get; set; } = new();

    /// <summary>
    /// Reserved for future options (e.g., SignalR, client options). Currently unused.
    /// </summary>
    public bool Reserved { get; set; }
}
