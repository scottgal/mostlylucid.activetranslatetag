using System.Text;
using Microsoft.Extensions.Logging;

namespace mostlylucid.llmtranslate.Services.Providers;

/// <summary>
/// Decorator that splits long inputs into chunks before delegating to the inner IAiTranslationProvider.
/// Works with character-length based chunking and optional overlap.
/// </summary>
public class ChunkingAiTranslationProvider : IAiTranslationProvider
{
    private readonly IAiTranslationProvider _inner;
    private readonly int _chunkLength;
    private readonly int _overlap;
    private readonly ILogger<ChunkingAiTranslationProvider> _logger;

    public ChunkingAiTranslationProvider(
        IAiTranslationProvider inner,
        int chunkLength,
        int overlap,
        ILogger<ChunkingAiTranslationProvider> logger)
    {
        _inner = inner;
        _chunkLength = Math.Max(1, chunkLength);
        _overlap = Math.Max(0, overlap);
        _logger = logger;
    }

    public async Task<string> TranslateAsync(string text, string targetLanguage, string? sourceLanguage = "en", CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(text)) return text;
        if (text.Length <= _chunkLength)
        {
            return await _inner.TranslateAsync(text, targetLanguage, sourceLanguage, ct);
        }

        var chunks = ChunkText(text, _chunkLength, _overlap);
        var translatedParts = new List<string>(chunks.Count);
        foreach (var chunk in chunks)
        {
            var part = await _inner.TranslateAsync(chunk, targetLanguage, sourceLanguage, ct);
            translatedParts.Add(part);
        }
        return string.Concat(translatedParts);
    }

    public async Task<Dictionary<string, string>> TranslateBatchAsync(Dictionary<string, string> items, string targetLanguage, string? sourceLanguage = "en", CancellationToken ct = default)
    {
        // Split only those entries that exceed chunk length; keep others as-is to leverage batch when possible.
        var small = new Dictionary<string, string>();
        var large = new Dictionary<string, string>();
        foreach (var kv in items)
        {
            if (!string.IsNullOrEmpty(kv.Value) && kv.Value.Length > _chunkLength)
                large[kv.Key] = kv.Value;
            else
                small[kv.Key] = kv.Value;
        }

        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (small.Count > 0)
        {
            var batchResult = await _inner.TranslateBatchAsync(small, targetLanguage, sourceLanguage, ct);
            foreach (var kv in batchResult) result[kv.Key] = kv.Value;
        }

        foreach (var kv in large)
        {
            result[kv.Key] = await TranslateAsync(kv.Value, targetLanguage, sourceLanguage, ct);
        }

        return result;
    }

    private static List<string> ChunkText(string text, int chunkLength, int overlap)
    {
        var chunks = new List<string>();
        if (string.IsNullOrEmpty(text)) return chunks;

        int start = 0;
        while (start < text.Length)
        {
            int length = Math.Min(chunkLength, text.Length - start);
            // Try to avoid breaking in the middle of a word by scanning backward to nearest whitespace if feasible
            int endExclusive = start + length;
            if (endExclusive < text.Length)
            {
                int lastSpace = text.LastIndexOfAny(new[] { ' ', '\n', '\r', '\t' }, endExclusive - 1, Math.Min(40, length));
                if (lastSpace > start + 10) // avoid tiny trailing fragment
                {
                    endExclusive = lastSpace + 1;
                }
            }
            chunks.Add(text.Substring(start, endExclusive - start));

            if (endExclusive >= text.Length) break;
            start = Math.Max(0, endExclusive - overlap);
        }

        return chunks;
    }
}
