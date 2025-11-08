using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using mostlylucid.activetranslatetag.Data;
using mostlylucid.activetranslatetag.Models;

namespace mostlylucid.activetranslatetag.Services;

/// <summary>
/// Translation service backed by EF Core database providers (PostgreSQL/Sqlite/SqlServer).
/// Provides request-level and memory caching on top of persistent storage.
/// </summary>
public class EfTranslationService : ITranslationService
{
    private readonly ITranslationDbContext _db;
    private readonly IAiTranslationProvider _ai;
    private readonly IMemoryCache _cache;
    private readonly RequestTranslationCache _requestCache;
    private readonly ILogger<EfTranslationService> _logger;

    private readonly bool _enableMemoryCache;
    private readonly int _cacheDurationMinutes;

    public EfTranslationService(
        ITranslationDbContext db,
        IAiTranslationProvider ai,
        IMemoryCache cache,
        RequestTranslationCache requestCache,
        ILogger<EfTranslationService> logger,
        bool enableMemoryCache = true,
        int cacheDurationMinutes = 60)
    {
        _db = db;
        _ai = ai;
        _cache = cache;
        _requestCache = requestCache;
        _logger = logger;
        _enableMemoryCache = enableMemoryCache;
        _cacheDurationMinutes = cacheDurationMinutes;
    }

    public async Task<string> GetAsync(string key, string languageCode, CancellationToken ct = default)
    {
        // Request-scope cache first
        if (_requestCache.TryGet(languageCode, key, out var reqCached) && reqCached != null)
            return reqCached;

        var cacheKey = $"trans:{languageCode}:{key}";
        if (_enableMemoryCache && _cache.TryGetValue<string>(cacheKey, out var cached))
        {
            _requestCache.Set(languageCode, key, cached!);
            return cached!;
        }

        // Get string
        var ts = await _db.TranslationStrings.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Key == key, ct);
        if (ts == null)
        {
            _logger.LogDebug("Translation string not found for key {Key}", key);
            return key; // fallback to key
        }

        if (string.Equals(languageCode, "en", StringComparison.OrdinalIgnoreCase))
        {
            if (_enableMemoryCache)
                _cache.Set(cacheKey, ts.DefaultText, TimeSpan.FromMinutes(_cacheDurationMinutes));
            _requestCache.Set(languageCode, key, ts.DefaultText);
            return ts.DefaultText;
        }

        var translation = await _db.Translations.AsNoTracking()
            .Where(t => t.TranslationStringId == ts.Id && t.LanguageCode == languageCode)
            .Select(t => t.TranslatedText)
            .FirstOrDefaultAsync(ct);

        var result = translation ?? ts.DefaultText;
        if (_enableMemoryCache)
            _cache.Set(cacheKey, result, TimeSpan.FromMinutes(_cacheDurationMinutes));
        _requestCache.Set(languageCode, key, result);
        return result;
    }

    public async Task<int> EnsureStringAsync(string key, string defaultText, string? category = null, string? context = null, CancellationToken ct = default)
    {
        var existing = await _db.TranslationStrings.FirstOrDefaultAsync(x => x.Key == key, ct);
        if (existing != null)
        {
            if (!string.Equals(existing.DefaultText, defaultText, StringComparison.Ordinal))
            {
                existing.DefaultText = defaultText;
                existing.UpdatedAtUtc = DateTime.UtcNow;
                await _db.SaveChangesAsync(ct);
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
        _db.TranslationStrings.Add(ts);
        await _db.SaveChangesAsync(ct);
        return ts.Id;
    }

    public async Task<int> TranslateAllStringsAsync(string targetLanguage, bool overwriteExisting = false, IProgress<TranslationProgress>? progress = null, CancellationToken ct = default)
    {
        var lang = targetLanguage.ToLowerInvariant();

        var strings = await _db.TranslationStrings.AsNoTracking().ToListAsync(ct);
        int translatedCount = 0;
        int total = strings.Count;
        int idx = 0;

        foreach (var s in strings)
        {
            idx++;
            ct.ThrowIfCancellationRequested();

            var hasExisting = await _db.Translations.AnyAsync(t => t.TranslationStringId == s.Id && t.LanguageCode == lang, ct);
            if (hasExisting && !overwriteExisting)
            {
                progress?.Report(new TranslationProgress(total, translatedCount, s.Key));
                continue;
            }

            var text = await _ai.TranslateAsync(s.DefaultText, lang, null, s.Context, ct);
            if (!string.IsNullOrWhiteSpace(text))
            {
                var entity = await _db.Translations.FirstOrDefaultAsync(t => t.TranslationStringId == s.Id && t.LanguageCode == lang, ct);
                if (entity == null)
                {
                    entity = new Translation
                    {
                        TranslationStringId = s.Id,
                        LanguageCode = lang,
                        TranslatedText = text,
                        CreatedAtUtc = DateTime.UtcNow,
                        UpdatedAtUtc = DateTime.UtcNow
                    };
                    _db.Translations.Add(entity);
                }
                else
                {
                    entity.TranslatedText = text;
                    entity.UpdatedAtUtc = DateTime.UtcNow;
                }
                await _db.SaveChangesAsync(ct);
                translatedCount++;
                progress?.Report(new TranslationProgress(total, translatedCount, s.Key));
            }
        }

        return translatedCount;
    }

    public async Task<List<string>> GetAvailableLanguagesAsync(CancellationToken ct = default)
    {
        var langs = await _db.Translations.AsNoTracking()
            .Select(t => t.LanguageCode)
            .Distinct()
            .ToListAsync(ct);
        langs.Sort(StringComparer.OrdinalIgnoreCase);
        return langs;
    }

    public async Task<TranslationStats> GetStatsAsync(string languageCode, CancellationToken ct = default)
    {
        var total = await _db.TranslationStrings.CountAsync(ct);
        var translated = await _db.Translations.CountAsync(t => t.LanguageCode == languageCode, ct);
        var remaining = Math.Max(0, total - translated);
        return new TranslationStats(languageCode, total, translated, remaining, 0);
    }

    public async Task<List<TranslationStringDto>> GetAllStringsWithTranslationsAsync(string languageCode, CancellationToken ct = default)
    {
        var lang = languageCode.ToLowerInvariant();
        var data = await _db.TranslationStrings
            .AsNoTracking()
            .Select(ts => new TranslationStringDto(
                ts.Key,
                ts.DefaultText,
                ts.Translations.Where(t => t.LanguageCode == lang).Select(t => t.TranslatedText).FirstOrDefault()
            ))
            .ToListAsync(ct);
        return data;
    }
}
