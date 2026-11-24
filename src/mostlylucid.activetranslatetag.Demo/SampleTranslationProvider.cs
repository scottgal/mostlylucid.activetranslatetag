using mostlylucid.activetranslatetag.Services;

namespace mostlylucid.activetranslatetag.Demo;

// A presentation fixture, not a translation engine. Used only by the no-download demo.
public sealed class SampleTranslationProvider : IAiTranslationProvider
{
    public async Task<string> TranslateAsync(string text, string targetLanguage, string? sourceLanguage = "en", string? description = null, CancellationToken ct = default)
    {
        await Task.Delay(900, ct);
        if (targetLanguage != "fr") throw new InvalidOperationException("Sample mode only includes French. Select a real LLM provider for other languages.");
        return text.Replace("Hello, forum!", "Bonjour, le forum!")
            .Replace("This post translates when the server is ready.", "Ce message est traduit lorsque le serveur est prêt.")
            .Replace("Welcome, {name}.", "Bienvenue, {name}.")
            .Replace("Read the", "Lisez le").Replace("forum guide", "guide du forum");
    }
    public async Task<Dictionary<string, string>> TranslateBatchAsync(Dictionary<string, string> items, string targetLanguage,
        string? sourceLanguage = "en", string? description = null, CancellationToken ct = default)
    {
        var result = new Dictionary<string, string>();
        foreach (var (key, text) in items) result[key] = await TranslateAsync(text, targetLanguage, sourceLanguage, description, ct);
        return result;
    }
}
