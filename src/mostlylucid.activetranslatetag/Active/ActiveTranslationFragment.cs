using System.Security.Cryptography;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace mostlylucid.activetranslatetag.Active;

internal static class ActiveTranslationFragment
{
    public const string Path = "/_active-translate/fragment";

    public static async Task<Results<ContentHttpResult, NoContent, BadRequest>> GetAsync(string? token, HttpResponse response,
        TranslationTokens tokens, SqliteTranslationStore store, CancellationToken ct)
    {
        response.Headers.CacheControl = "no-store";
        string id;
        try { id = tokens.Read(token ?? string.Empty); }
        catch (Exception ex) when (ex is CryptographicException or ArgumentException)
        {
            response.Headers["HX-Reswap"] = "none";
            return TypedResults.BadRequest();
        }
        var html = await store.GetHtmlAsync(id, ct);
        if (html == null) return TypedResults.NoContent();
        response.Headers["HX-Trigger"] = "translation-swapped";
        return TypedResults.Content(html, "text/html; charset=utf-8");
    }
}
