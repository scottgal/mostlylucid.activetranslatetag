using mostlylucid.llmtranslate.Services;

namespace mostlylucid.llmtranslate.Tests;

public class FakeAiProvider : IAiTranslationProvider
{
    public Task<string> TranslateAsync(string text, string targetLanguage, string? sourceLanguage = "en", CancellationToken ct = default)
        => Task.FromResult($"{text}<{targetLanguage}>");

    public Task<Dictionary<string, string>> TranslateBatchAsync(Dictionary<string, string> items, string targetLanguage, string? sourceLanguage = "en", CancellationToken ct = default)
        => Task.FromResult(items.ToDictionary(kv => kv.Key, kv => $"{kv.Value}<{targetLanguage}>") );
}