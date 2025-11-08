using Microsoft.AspNetCore.Http;
using mostlylucid.activetranslatetag.Services;

namespace mostlylucid.activetranslatetag.Helpers;

/// <summary>
/// Helper class for accessing translations in views
/// </summary>
public class TranslationHelper
{
    private readonly ITranslationService _translationService;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public TranslationHelper(ITranslationService translationService, IHttpContextAccessor httpContextAccessor)
    {
        _translationService = translationService;
        _httpContextAccessor = httpContextAccessor;
    }

    /// <summary>
    /// Get the current user's preferred language from cookie or default to English
    /// </summary>
    public string GetCurrentLanguage()
    {
        var context = _httpContextAccessor.HttpContext;
        if (context == null) return "en";

        // Try to get language from cookie
        if (context.Request.Cookies.TryGetValue("preferred-language", out var lang))
            return lang;

        // Try to get from Accept-Language header
        var acceptLanguage = context.Request.Headers.AcceptLanguage.FirstOrDefault();
        if (!string.IsNullOrEmpty(acceptLanguage))
        {
            var primaryLang = acceptLanguage.Split(',').FirstOrDefault()?.Split(';').FirstOrDefault()?.Trim();
            if (!string.IsNullOrEmpty(primaryLang))
            {
                // Extract just the language code (e.g., "en" from "en-US")
                var langCode = primaryLang.Split('-').FirstOrDefault();
                if (!string.IsNullOrEmpty(langCode))
                    return langCode.ToLowerInvariant();
            }
        }

        return "en";
    }

    /// <summary>
    /// Translate a string using the current user's language
    /// </summary>
    /// <param name="key">Translation key</param>
    /// <param name="defaultText">Default text to use if translation not found</param>
    /// <param name="description">Optional context description to improve translation quality (used by LLM providers only)</param>
    public async Task<string> T(string key, string? defaultText = null, string? description = null)
    {
        var language = GetCurrentLanguage();

        // If default text provided and key doesn't exist, ensure it's registered
        if (defaultText != null)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    await _translationService.EnsureStringAsync(key, defaultText, context: description);
                }
                catch
                {
                    // Best effort - don't block rendering
                }
            });
        }

        return await _translationService.GetAsync(key, language);
    }

    /// <summary>
    /// Translate a string with a specific language
    /// </summary>
    /// <param name="key">Translation key</param>
    /// <param name="languageCode">Target language code</param>
    /// <param name="defaultText">Default text to use if translation not found</param>
    /// <param name="description">Optional context description to improve translation quality (used by LLM providers only)</param>
    public async Task<string> T(string key, string languageCode, string? defaultText = null, string? description = null)
    {
        if (defaultText != null)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    await _translationService.EnsureStringAsync(key, defaultText, context: description);
                }
                catch
                {
                    // Best effort
                }
            });
        }

        return await _translationService.GetAsync(key, languageCode);
    }
}
