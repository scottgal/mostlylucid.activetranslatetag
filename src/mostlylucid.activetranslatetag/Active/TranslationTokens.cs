using Microsoft.AspNetCore.DataProtection;

namespace mostlylucid.activetranslatetag.Active;

// A short-lived capability for exactly the content already rendered to this browser.
// The URL contains no forum text, markup, model prompt or arbitrary translation input.
public sealed class TranslationTokens(IDataProtectionProvider provider)
{
    private readonly ITimeLimitedDataProtector _protector = provider.CreateProtector("ActiveTranslation.Fragment.v1").ToTimeLimitedDataProtector();
    public string Create(string id) => _protector.Protect(id, TimeSpan.FromHours(12));
    public string Read(string token) => _protector.Unprotect(token);
}
