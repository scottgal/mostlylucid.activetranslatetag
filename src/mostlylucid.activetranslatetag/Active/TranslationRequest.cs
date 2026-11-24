using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace mostlylucid.activetranslatetag.Active;

public sealed record TranslationRequest(
    TranslationMarkup Markup, string SourceLanguage, string TargetLanguage, string? Description, string CacheNamespace)
{
    public string Id => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { Markup, SourceLanguage, TargetLanguage, Description, CacheNamespace }))));
}

public sealed record TranslationReady(string Html);

public sealed record TranslationFailed(string Message);

public union TranslationResult(TranslationReady, TranslationFailed);
