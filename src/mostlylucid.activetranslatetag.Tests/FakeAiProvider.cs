using mostlylucid.activetranslatetag.Services;

namespace mostlylucid.activetranslatetag.Tests;

public class FakeAiProvider : IAiTranslationProvider
{
    public Task<string> TranslateAsync(string text, string targetLanguage, string? sourceLanguage = "en", string? description = null, CancellationToken ct = default)
        => Task.FromResult($"{text}<{targetLanguage}>");

    public Task<Dictionary<string, string>> TranslateBatchAsync(Dictionary<string, string> items, string targetLanguage, string? sourceLanguage = "en", string? description = null, CancellationToken ct = default)
        => Task.FromResult(items.ToDictionary(kv => kv.Key, kv => $"{kv.Value}<{targetLanguage}>") );
}