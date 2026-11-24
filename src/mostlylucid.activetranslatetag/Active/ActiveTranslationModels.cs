using Microsoft.AspNetCore.Html;

namespace mostlylucid.activetranslatetag.Active;

// Html is already-rendered Razor child content. Encode plain user text before constructing this directly.
public sealed record ActiveTranslationInput(string Html, string Language = "fr", string SourceLanguage = "en",
    string? Description = null, string Mode = "active", string? Id = null, string? CssClass = null);

public sealed record ActiveTranslationViewModel(string Id, IHtmlContent Content, string Language,
    string TargetLanguage, string State, string? TranslationId, string? Token, string? FragmentUrl, string? CssClass);
