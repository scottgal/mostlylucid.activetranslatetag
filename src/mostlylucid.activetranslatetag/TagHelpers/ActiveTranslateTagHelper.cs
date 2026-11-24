using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;
using mostlylucid.activetranslatetag.Active;

namespace mostlylucid.activetranslatetag.TagHelpers;

/// <summary>The friendly wrapper syntax. The View Component and its Razor view do the rendering.</summary>
[HtmlTargetElement("active-translate")]
public sealed class ActiveTranslateTagHelper(IViewComponentHelper components) : TagHelper
{
    public string Language { get; set; } = "fr";
    public string SourceLanguage { get; set; } = "en";
    public string? Description { get; set; }
    public string Mode { get; set; } = "active";
    public string? Id { get; set; }
    [HtmlAttributeName("class")]
    public string? CssClass { get; set; }

    [ViewContext, HtmlAttributeNotBound]
    public ViewContext ViewContext { get; set; } = null!;

    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        var content = await output.GetChildContentAsync();
        ((IViewContextAware)components).Contextualize(ViewContext);
        var model = new ActiveTranslationInput(content.GetContent(), Language, SourceLanguage, Description, Mode, Id, CssClass);
        var rendered = await components.InvokeAsync("ActiveTranslate", new { model });
        output.TagName = null;
        output.Content.SetHtmlContent(rendered);
    }
}
