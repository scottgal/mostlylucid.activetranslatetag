using System.Collections.Concurrent;
using mostlylucid.activetranslatetag.Models;

namespace mostlylucid.activetranslatetag.Services.InMemory;

/// <summary>
/// Thread-safe in-memory store for TranslationString and Translation entities.
/// Intended for demos, tests, and ephemeral deployments. No persistence.
/// </summary>
public class InMemoryStore
{
    private readonly ConcurrentDictionary<string, TranslationString> _stringsByKey = new(StringComparer.Ordinal);
    private int _nextId = 1;

    public Task<TranslationString?> GetStringAsync(string key, CancellationToken ct = default)
    {
        _stringsByKey.TryGetValue(key, out var value);
        return Task.FromResult(value);
    }

    public Task<List<TranslationString>> GetAllStringsAsync(CancellationToken ct = default)
    {
        var list = _stringsByKey.Values.ToList();
        return Task.FromResult(list);
    }

    public Task<int> UpsertStringAsync(TranslationString translationString, CancellationToken ct = default)
    {
        if (translationString.Id == 0)
        {
            translationString.Id = Interlocked.Increment(ref _nextId);
            translationString.CreatedAtUtc = translationString.CreatedAtUtc == default ? DateTime.UtcNow : translationString.CreatedAtUtc;
        }
        translationString.UpdatedAtUtc = DateTime.UtcNow;
        _stringsByKey.AddOrUpdate(translationString.Key, translationString, (k, existing) =>
        {
            existing.DefaultText = translationString.DefaultText;
            existing.Context = translationString.Context;
            existing.Category = translationString.Category;
            existing.UpdatedAtUtc = DateTime.UtcNow;
            return existing;
        });
        return Task.FromResult(translationString.Id);
    }

    public Task<Translation?> GetTranslationAsync(string key, string languageCode, CancellationToken ct = default)
    {
        if (_stringsByKey.TryGetValue(key, out var ts))
        {
            var t = ts.Translations.FirstOrDefault(x => x.LanguageCode == languageCode);
            return Task.FromResult(t);
        }
        return Task.FromResult<Translation?>(null);
    }

    public Task UpsertTranslationAsync(int translationStringId, Translation translation, CancellationToken ct = default)
    {
        // Find by Id
        var ts = _stringsByKey.Values.FirstOrDefault(x => x.Id == translationStringId);
        if (ts == null) return Task.CompletedTask;

        var existing = ts.Translations.FirstOrDefault(x => x.LanguageCode == translation.LanguageCode);
        if (existing != null)
        {
            existing.TranslatedText = translation.TranslatedText;
            existing.AiModel = translation.AiModel;
            existing.UpdatedAtUtc = DateTime.UtcNow;
        }
        else
        {
            translation.Id = 0; // not used in memory
            translation.TranslationStringId = translationStringId;
            translation.CreatedAtUtc = DateTime.UtcNow;
            translation.UpdatedAtUtc = DateTime.UtcNow;
            ts.Translations.Add(translation);
        }
        return Task.CompletedTask;
    }
}
