using System.Net.Http.Json;
using System.Text.Json;

namespace mostlylucid.activetranslatetag.Services.Providers;

// Strict provider for the active pipeline: failures must not be cached as successful translations.
public sealed class LocalOllamaTranslationProvider(HttpClient http, string baseUrl, string model) : IAiTranslationProvider
{
    public async Task<string> TranslateAsync(string text, string targetLanguage, string? sourceLanguage = "en", string? description = null, CancellationToken ct = default)
    {
        using var response = await http.PostAsJsonAsync(new Uri(new Uri(baseUrl), "api/chat"), new
        {
            model, stream = false, think = false,
            messages = new[] {
                new { role = "system", content = TranslationPrompt.System },
                new { role = "user", content = TranslationPrompt.User(text, targetLanguage, sourceLanguage, description) }
            },
            options = new { temperature = 0.2, num_predict = 1024 }
        }, ct);
        response.EnsureSuccessStatusCode();
        using var json = await response.Content.ReadFromJsonAsync<JsonDocument>(cancellationToken: ct)
            ?? throw new InvalidOperationException("Empty Ollama response.");
        if (json.RootElement.TryGetProperty("done_reason", out var reason) && reason.GetString() == "length")
            throw new InvalidOperationException("Translation exceeded the output token budget.");
        return TranslationPrompt.ValidateOutput(json.RootElement.GetProperty("message").GetProperty("content").GetString() ?? "");
    }

    public async Task<Dictionary<string, string>> TranslateBatchAsync(Dictionary<string, string> items, string targetLanguage,
        string? sourceLanguage = "en", string? description = null, CancellationToken ct = default)
    {
        var result = new Dictionary<string, string>();
        foreach (var (key, text) in items) result[key] = await TranslateAsync(text, targetLanguage, sourceLanguage, description, ct);
        return result;
    }
}
