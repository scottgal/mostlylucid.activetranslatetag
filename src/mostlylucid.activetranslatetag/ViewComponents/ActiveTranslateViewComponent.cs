using System.Globalization;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using mostlylucid.activetranslatetag.Active;

namespace mostlylucid.activetranslatetag.ViewComponents;

public sealed class ActiveTranslateViewComponent(SqliteTranslationStore store, ActiveTranslationQueue queue,
    TranslationTokens tokens, IOptions<ActiveTranslationOptions> options) : ViewComponent
{
    public async Task<IViewComponentResult> InvokeAsync(ActiveTranslationInput model)
    {
        var ct = HttpContext.RequestAborted;
        var markup = TranslationMarkup.Parse(model.Html);
        var sourceLanguage = CultureInfo.GetCultureInfo(model.SourceLanguage).Name.ToLowerInvariant();
        var targetLanguage = CultureInfo.GetCultureInfo(model.Language).Name.ToLowerInvariant();
        if (string.IsNullOrEmpty(sourceLanguage) || string.IsNullOrEmpty(targetLanguage))
            throw new ArgumentException("Source and target languages must be valid language tags.");
        if (model.Mode is not ("active" or "server")) throw new ArgumentException("Mode must be active or server.");
        var elementId = model.Id ?? $"translation-{Guid.NewGuid():N}";
        if (string.IsNullOrWhiteSpace(markup.Text) || sourceLanguage == targetLanguage)
            return View(new ActiveTranslationViewModel(elementId, new HtmlString(model.Html), sourceLanguage,
                targetLanguage, "ready", null, null, null, model.CssClass));

        var request = new TranslationRequest(markup, sourceLanguage, targetLanguage, model.Description, options.Value.CacheNamespace);
        var html = await store.GetHtmlAsync(request.Id, ct);
        var failed = false;
        if (html == null)
        {
            await store.SaveRequestAsync(request, ct);
            var work = await queue.EnqueueAsync(request.Id, ct);
            if (model.Mode == "server" || work.IsCompletedSuccessfully)
            {
                var result = await work.WaitAsync(ct);
                (html, failed) = result switch
                {
                    TranslationReady ready => (ready.Html, false),
                    TranslationFailed => ((string?)null, true)
                };
            }
        }
        var pending = html == null && !failed;
        var token = pending ? tokens.Create(request.Id) : null;
        var url = token == null ? null : QueryHelpers.AddQueryString(HttpContext.Request.PathBase + ActiveTranslationFragment.Path, "token", token);
        return View(new ActiveTranslationViewModel(elementId, new HtmlString(html ?? model.Html),
            html == null ? sourceLanguage : targetLanguage, targetLanguage,
            pending ? "pending" : failed ? "failed" : "ready", request.Id, token, url, model.CssClass));
    }
}
