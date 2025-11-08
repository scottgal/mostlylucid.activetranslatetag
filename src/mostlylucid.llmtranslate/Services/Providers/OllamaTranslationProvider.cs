using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace mostlylucid.llmtranslate.Services.Providers;

/// <summary>
/// IAiTranslationProvider implementation that calls a local/remote Ollama server.
/// Uses the /api/generate endpoint with a translation prompt.
/// </summary>
public class OllamaTranslationProvider : IAiTranslationProvider
{
    private readonly HttpClient _http;
    private readonly ILogger<OllamaTranslationProvider> _logger;
    private readonly string _model;

    /// <param name="baseUrl">Base URL of the Ollama server, e.g. http://localhost:11434/</param>
    /// <param name="model">Model to use, e.g. "llama3.1"</param>
    public OllamaTranslationProvider(HttpClient httpClient, ILogger<OllamaTranslationProvider> logger, string? baseUrl = null, string model = "llama3.1")
    {
        _http = httpClient;
        _logger = logger;
        _model = string.IsNullOrWhiteSpace(model) ? "llama3.1" : model;
        if (!string.IsNullOrWhiteSpace(baseUrl))
        {
            _http.BaseAddress = new Uri(baseUrl);
        }
        if (_http.BaseAddress == null)
        {
            _http.BaseAddress = new Uri("http://localhost:11434/");
        }
    }

    public async Task<string> TranslateAsync(string text, string targetLanguage, string? sourceLanguage = "en", CancellationToken ct = default)
    {
        try
        {
            var prompt = BuildPrompt(text, targetLanguage, sourceLanguage);
            using var resp = await _http.PostAsJsonAsync("api/generate", new
            {
                model = _model,
                prompt,
                stream = false
            }, ct);
            if (!resp.IsSuccessStatusCode)
            {
                var err = await resp.Content.ReadAsStringAsync(ct);
                _logger.LogWarning("Ollama /api/generate failed: {Status} {Error}", resp.StatusCode, err);
                return text; // fail soft
            }
            var doc = await resp.Content.ReadFromJsonAsync<JsonDocument>(cancellationToken: ct);
            var content = doc?.RootElement.TryGetProperty("response", out var respText) == true ? respText.GetString() : null;
            return string.IsNullOrWhiteSpace(content) ? text : content!;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ollama translation failed");
            return text;
        }
    }

    public async Task<Dictionary<string, string>> TranslateBatchAsync(Dictionary<string, string> items, string targetLanguage, string? sourceLanguage = "en", CancellationToken ct = default)
    {
        // Ollama doesn't have a native batch translate: we can construct a JSON-guided prompt, but keep simple and robust.
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in items)
        {
            result[key] = await TranslateAsync(value, targetLanguage, sourceLanguage, ct);
        }
        return result;
    }

    private static string BuildPrompt(string text, string targetLanguage, string? sourceLanguage)
    {
        var src = string.IsNullOrWhiteSpace(sourceLanguage) ? "auto" : sourceLanguage;
        return $"Translate the following text from {src} to {targetLanguage}. Preserve placeholders like {{0}} or {{name}}, and preserve HTML tags as-is.\n\nText:\n{text}";
    }
}
