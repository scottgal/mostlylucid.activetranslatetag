using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using HtmlAgilityPack;
using mostlylucid.activetranslatetag.Services;

namespace mostlylucid.activetranslatetag.Active;

// Only original, server-rendered markup may become HTML. Model output is always encoded.
public sealed partial record TranslationMarkup(string Text, Dictionary<string, string> Tags)
{
    public static TranslationMarkup Parse(string html)
    {
        var document = new HtmlDocument();
        document.LoadHtml(html);
        var tags = new Dictionary<string, string>();
        var text = new StringBuilder();
        var prefix = "HTML";
        while (html.Contains($"[[{prefix}_", StringComparison.Ordinal)) prefix += "_";
        string Keep(string value)
        {
            var token = $"[[{prefix}_{tags.Count}]]";
            tags.Add(token, value);
            return token;
        }
        void Visit(HtmlNode node)
        {
            if (node.NodeType == HtmlNodeType.Text)
            {
                var value = HtmlEntity.DeEntitize(node.InnerText);
                text.Append(value);
            }
            else if (node.NodeType == HtmlNodeType.Element)
            {
                if (node.Name is "script" or "style" || node.Attributes.Contains("data-translation-id"))
                    throw new ArgumentException("Active translation must contain text and inert markup; nested active tags, script and style are unsupported.");
                // Derive the opening tag from the parsed node; attributes remain server-authored.
                var clone = node.CloneNode(false);
                clone.RemoveAllChildren();
                var outer = clone.OuterHtml;
                text.Append(Keep(OpeningTag().Match(outer).Value));
                foreach (var child in node.ChildNodes) Visit(child);
                if (!HtmlNode.IsEmptyElement(node.Name)) text.Append(Keep($"</{node.Name}>"));
            }
        }
        foreach (var node in document.DocumentNode.ChildNodes) Visit(node);
        return new TranslationMarkup(text.ToString(), tags);
    }

    // A '>' inside a quoted attribute is part of its value.
    [GeneratedRegex("""^<(?:[^>"']|"[^"]*"|'[^']*')*>""")]
    private static partial Regex OpeningTag();

    // Keep HTML markers on the server. Tiny models need only translate actual text nodes.
    public async Task<string> TranslateAsync(IAiTranslationProvider provider, string targetLanguage,
        string sourceLanguage, string? description, CancellationToken ct)
    {
        var output = new StringBuilder();
        foreach (var part in Regex.Split(Text, @"(\[\[HTML_+\d+\]\])"))
        {
            ct.ThrowIfCancellationRequested();
            if (Tags.ContainsKey(part) || !part.Any(char.IsLetter) || Regex.IsMatch(part.Trim(), @"^\{[^{}]+\}$"))
            {
                output.Append(part);
                continue;
            }
            var text = part.Trim();
            var translated = (await provider.TranslateAsync(text, targetLanguage, sourceLanguage, description, ct)).Trim();
            // Validate variables per fragment before reinserting the untouched structural markers.
            new TranslationMarkup(text, []).Validate(translated);
            var leading = part.Length - part.TrimStart().Length;
            var trailing = part.Length - part.TrimEnd().Length;
            output.Append(part.AsSpan(0, leading));
            output.Append(translated);
            if (trailing > 0) output.Append(part.AsSpan(part.Length - trailing));
        }
        return Render(output.ToString());
    }

    public string Render(string translation)
    {
        Validate(translation);
        var encoded = WebUtility.HtmlEncode(translation);
        foreach (var (token, tag) in Tags) encoded = encoded.Replace(token, tag, StringComparison.Ordinal);
        return encoded;
    }

    public void Validate(string translation)
    {
        if (string.IsNullOrWhiteSpace(translation)) throw new InvalidOperationException("The model returned empty text.");
        // Also preserve user placeholders such as {name}, {0}, and template markers.
        var pattern = @"\[\[HTML_+\d+\]\]|\{[^{}]+\}";
        var expected = Regex.Matches(Text, pattern).Select(m => m.Value).Order().ToArray();
        var actual = Regex.Matches(translation, pattern).Select(m => m.Value).Order().ToArray();
        if (!expected.SequenceEqual(actual)) throw new InvalidOperationException("The model changed translation placeholders.");
        // Reordering tag boundaries can corrupt nesting, even when every token is present.
        var tagPattern = @"\[\[HTML_+\d+\]\]";
        if (!Regex.Matches(Text, tagPattern).Select(m => m.Value)
            .SequenceEqual(Regex.Matches(translation, tagPattern).Select(m => m.Value)))
            throw new InvalidOperationException("The model reordered HTML boundaries.");
    }
}
