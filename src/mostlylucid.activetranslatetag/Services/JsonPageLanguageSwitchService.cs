using System.Text;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using mostlylucid.activetranslatetag.Hubs;

namespace mostlylucid.activetranslatetag.Services;

/// <summary>
/// Language switch service for JSON-file backed storage that does not depend on a DbContext.
/// It renders OOB swaps for requested keys via ITranslationService and updates the current language indicator.
/// Optionally, a background translation job for all strings can be triggered by calling TranslateAllStringsAsync
/// elsewhere (e.g., admin action). This class keeps runtime dependencies minimal for file-based deployments.
/// </summary>
internal sealed class JsonPageLanguageSwitchService : IPageLanguageSwitchService
{
    private readonly ITranslationService _translationService;
    private readonly IHubContext<TranslationHub> _hubContext;
    private readonly ILogger<JsonPageLanguageSwitchService> _logger;

    public JsonPageLanguageSwitchService(
        ITranslationService translationService,
        IHubContext<TranslationHub> hubContext,
        ILogger<JsonPageLanguageSwitchService> logger)
    {
        _translationService = translationService;
        _hubContext = hubContext;
        _logger = logger;
    }

    public async Task<string> BuildSwitchResponseAsync(string languageCode, IEnumerable<string> keys, CancellationToken ct = default)
    {
        var html = new StringBuilder();
        var distinctKeys = keys?.Distinct().ToList() ?? new List<string>();

        if (distinctKeys.Count == 0)
        {
            html.AppendLine($"<span id=\"current-lang\" hx-swap-oob=\"innerHTML\">{languageCode.ToUpperInvariant()}</span>");
            return html.ToString();
        }

        foreach (var key in distinctKeys)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var text = await _translationService.GetAsync(key, languageCode, ct);
                var elementId = $"t-{Helpers.ContentHash.Generate(key)}";
                html.AppendLine($"<span id=\"{elementId}\" hx-swap-oob=\"innerHTML\">{System.Net.WebUtility.HtmlEncode(text)}</span>");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to fetch translation for key {Key} in {Lang}", key, languageCode);
            }
        }

        // Always update current language indicator
        html.AppendLine($"<span id=\"current-lang\" hx-swap-oob=\"innerHTML\">{languageCode.ToUpperInvariant()}</span>");

        return html.ToString();
    }
}
