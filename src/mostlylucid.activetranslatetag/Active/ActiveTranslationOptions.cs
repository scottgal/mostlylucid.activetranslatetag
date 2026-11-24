namespace mostlylucid.activetranslatetag.Active;

public sealed class ActiveTranslationOptions
{
    public string DatabasePath { get; set; } = "App_Data/active-translations.db";
    // Change this when changing model, quantization, or prompt to avoid stale results.
    public string CacheNamespace { get; set; } = "translation-v1";
    public int MemoryCacheEntries { get; set; } = 512;
    public int QueueCapacity { get; set; } = 128;
    public TimeSpan TranslationTimeout { get; set; } = TimeSpan.FromMinutes(2);
}
