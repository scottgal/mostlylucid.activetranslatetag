using Microsoft.AspNetCore.Mvc;

namespace mostlylucid.activetranslatetag.ViewComponents;

// Presentation belongs in Razor. The Tag Helper handles content transformation instead.
public sealed class ActiveTranslationStatusViewComponent : ViewComponent
{
    public IViewComponentResult Invoke() => View();
}
