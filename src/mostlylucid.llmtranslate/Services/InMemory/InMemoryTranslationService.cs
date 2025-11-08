using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using mostlylucid.llmtranslate.Models;

namespace mostlylucid.llmtranslate.Services.InMemory;

/// <summary>
/// Translation service implementation using in-memory volatile store.
/// Great for demos/tests/CI where persistence isn't required.
/// </summary>
public class InMemoryTranslationService : ITranslationService
{
    private readonly InMemoryStore _store;
    private readonly IAiTranslationProvider _ai;
    private readonly IMemoryCache _cache;
    private readonly RequestTranslationCache _requestCache;
    private readonly ILogger<InMemoryTranslationService> _logger;
    private readonly bool _enableMemoryCache;
    private readonly int _cacheDurationMinutes;

    public InMemoryTranslationService(
        InMemoryStore store,
        IAiTranslationProvider ai,
        IMemoryCache cache,
        RequestTranslationCache requestCache,
        ILogger<InMemoryTranslationService> logger,
        bool enableMemoryCache = true,
        int cacheDurationMinutes = 60)
    {
        _store = store;
        _ai = ai;
        _cache = cache;
        _requestCache = requestCache;
        _logger = logger;
        _enableMemoryCache = enableMemoryCache;
        _cacheDurationMinutes = cacheDurationMinutes;
    }

    public async Task<string> GetAsync(string key, string languageCode, CancellationToken ct = default)
    {
        if (_requestCache.TryGet(languageCode, key, out var reqCached) && reqCached != null)
            return reqCached;

        var cacheKey = $"trans:{languageCode}:{key}";
        if (_enableMemoryCache && _cache.TryGetValue<string>(cacheKey, out var cached))
        {
            _requestCache.Set(languageCode, key, cached!);
            return cached!;
        }

        var ts = await _store.GetStringAsync(key, ct);
        if (ts == null)
        {
            _logger.LogDebug("Translation string not found for key {Key}", key);
            return key;
        }

        if (string.Equals(languageCode, "en", StringComparison.OrdinalIgnoreCase))
        {
            if (_enableMemoryCache)
                _cache.Set(cacheKey, ts.DefaultText, TimeSpan.FromMinutes(_cacheDurationMinutes));
            _requestCache.Set(languageCode, key, ts.DefaultText);
            return ts.DefaultText;
        }

        var translation = await _store.GetTranslationAsync(key, languageCode, ct);
        var result = translation?.TranslatedText ?? ts.DefaultText;
        if (_enableMemoryCache)
            _cache.Set(cacheKey, result, TimeSpan.FromMinutes(_cacheDurationMinutes));
        _requestCache.Set(languageCode, key, result);
        return result;
    }

    public async Task<int> EnsureStringAsync(string key, string defaultText, string? category = null, string? context = null, CancellationToken ct = default)
    {
        var existing = await _store.GetStringAsync(key, ct);
        if (existing != null)
        {
            if (!string.Equals(existing.DefaultText, defaultText, StringComparison.Ordinal))
            {
                existing.DefaultText = defaultText;
                existing.UpdatedAtUtc = DateTime.UtcNow;
                await _store.UpsertStringAsync(existing, ct);
            }
            return existing.Id;
        }

        var ts = new TranslationString
        {
            Key = key,
            DefaultText = defaultText,
            Category = category,
            Context = context,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };
        return await _store.UpsertStringAsync(ts, ct);
    }

    public async Task<int> TranslateAllStringsAsync(string targetLanguage, bool overwriteExisting = false, IProgress<TranslationProgress>? progress = null, CancellationToken ct = default)
    {
        var lang = targetLanguage.ToLowerInvariant();
        var strings = await _store.GetAllStringsAsync(ct);
        int translated = 0;
        int total = strings.Count;
        int completed = 0;
        foreach (var s in strings)
        {
            completed++;
            var existing = await _store.GetTranslationAsync(s.Key, lang, ct);
            if (existing != null && !overwriteExisting)
            {
                progress?.Report(new TranslationProgress(total, translated, s.Key));
                continue;
            }
            var text = await _ai.TranslateAsync(s.DefaultText, lang, null, ct);
            if (!string.IsNullOrWhiteSpace(text))
            {
                await _store.UpsertTranslationAsync(s.Id, new Translation
                {
                    LanguageCode = lang,
                    TranslatedText = text,
                    CreatedAtUtc = DateTime.UtcNow,
                    UpdatedAtUtc = DateTime.UtcNow
                }, ct);
                translated++;
                progress?.Report(new TranslationProgress(total, translated, s.Key));
            }
        }
        return translated;
    }

    public async Task<List<string>> GetAvailableLanguagesAsync(CancellationToken ct = default)
    {
        var strings = await _store.GetAllStringsAsync(ct);
        var langs = strings.SelectMany(ts => ts.Translations.Select(t => t.LanguageCode)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        langs.Sort(StringComparer.OrdinalIgnoreCase);
        return langs;
    }

    public async Task<TranslationStats> GetStatsAsync(string languageCode, CancellationToken ct = default)
    {
        var strings = await _store.GetAllStringsAsync(ct);
        var total = strings.Count;
        var translated = strings.Sum(ts => ts.Translations.Any(t => t.LanguageCode == languageCode) ? 1 : 0);
        var remaining = Math.Max(0, total - translated);
        return new TranslationStats(languageCode, total, translated, remaining, total == 0 ? 0 : (double)translated / total * 100);
    }

    public async Task<List<TranslationStringDto>> GetAllStringsWithTranslationsAsync(string languageCode, CancellationToken ct = default)
    {
        var strings = await _store.GetAllStringsAsync(ct);
        var lang = languageCode.ToLowerInvariant();
        return strings.Select(ts => new TranslationStringDto(
            ts.Key,
            ts.DefaultText,
            ts.Translations.FirstOrDefault(t => t.LanguageCode == lang)?.TranslatedText
        )).ToList();
    }
}
