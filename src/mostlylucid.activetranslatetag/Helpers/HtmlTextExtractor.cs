using System.Text;
using System.Text.RegularExpressions;
using HtmlAgilityPack;

namespace mostlylucid.activetranslatetag.Helpers;

/// <summary>
/// Extracts plain text from HTML content while preserving structure for translation
/// </summary>
public static partial class HtmlTextExtractor
{
    // Self-closing tags that should be preserved
    private static readonly HashSet<string> SelfClosingTags = new(StringComparer.OrdinalIgnoreCase)
    {
        "img", "br", "hr", "input", "meta", "link", "area", "base", "col", "embed", "param", "source", "track", "wbr"
    };

    // Block-level elements that should add line breaks
    private static readonly HashSet<string> BlockElements = new(StringComparer.OrdinalIgnoreCase)
    {
        "p", "div", "h1", "h2", "h3", "h4", "h5", "h6", "ul", "ol", "li", "blockquote", "pre", "table", "tr", "section", "article", "aside", "header", "footer", "nav"
    };

    /// <summary>
    /// Extracts plain text from HTML content
    /// </summary>
    public static string ExtractText(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
            return html ?? string.Empty;

        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        // Remove script and style tags with their content
        var scriptNodes = doc.DocumentNode.SelectNodes("//script");
        if (scriptNodes != null)
        {
            foreach (var node in scriptNodes)
            {
                node.Remove();
            }
        }

        var styleNodes = doc.DocumentNode.SelectNodes("//style");
        if (styleNodes != null)
        {
            foreach (var node in styleNodes)
            {
                node.Remove();
            }
        }

        // Remove HTML comments
        var commentNodes = doc.DocumentNode.SelectNodes("//comment()");
        if (commentNodes != null)
        {
            foreach (var node in commentNodes)
            {
                node.Remove();
            }
        }

        // Extract text with line breaks for block elements
        var sb = new StringBuilder();
        ExtractTextRecursive(doc.DocumentNode, sb);

        var result = sb.ToString();

        // Decode HTML entities
        result = System.Net.WebUtility.HtmlDecode(result);

        // Normalize whitespace
        result = MultipleSpacesRegex().Replace(result, " ");
        result = MultipleNewlinesRegex().Replace(result, "\n\n");

        return result.Trim();
    }

    private static void ExtractTextRecursive(HtmlNode node, StringBuilder sb)
    {
        foreach (var child in node.ChildNodes)
        {
            if (child.NodeType == HtmlNodeType.Text)
            {
                sb.Append(child.InnerText);
            }
            else if (child.NodeType == HtmlNodeType.Element)
            {
                // Add line break before block element
                if (BlockElements.Contains(child.Name))
                {
                    sb.Append('\n');
                }

                ExtractTextRecursive(child, sb);

                // Add line break after block element
                if (BlockElements.Contains(child.Name))
                {
                    sb.Append('\n');
                }
            }
        }
    }

    /// <summary>
    /// Extracts text but preserves important HTML structure for re-injection
    /// Returns both the extracted text and a template for re-insertion
    /// </summary>
    public static (string PlainText, List<HtmlPlaceholder> Placeholders) ExtractWithPlaceholders(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
            return (html ?? string.Empty, new List<HtmlPlaceholder>());

        var placeholders = new List<HtmlPlaceholder>();
        var processed = html;

        var doc = new HtmlDocument();
        doc.LoadHtml(processed);

        // Remove script and style tags
        var scriptNodes = doc.DocumentNode.SelectNodes("//script");
        if (scriptNodes != null)
        {
            foreach (var node in scriptNodes)
            {
                node.Remove();
            }
        }

        var styleNodes = doc.DocumentNode.SelectNodes("//style");
        if (styleNodes != null)
        {
            foreach (var node in styleNodes)
            {
                node.Remove();
            }
        }

        processed = doc.DocumentNode.OuterHtml;

        // Extract and replace self-closing tags with placeholders using regex
        // (HtmlAgilityPack already parsed, but we need to work with the string for placeholders)
        var selfClosingMatches = SelfClosingTagRegex().Matches(processed);
        for (int i = selfClosingMatches.Count - 1; i >= 0; i--)
        {
            var match = selfClosingMatches[i];
            var placeholder = $"{{#{i}#}}";
            placeholders.Insert(0, new HtmlPlaceholder
            {
                Index = i,
                OriginalHtml = match.Value,
                Placeholder = placeholder,
                IsInline = true
            });
            processed = processed.Remove(match.Index, match.Length).Insert(match.Index, placeholder);
        }

        // Extract text
        var plainText = ExtractText(processed);

        return (plainText, placeholders);
    }

    /// <summary>
    /// Re-injects HTML tags back into translated text using placeholders
    /// </summary>
    public static string ReinjectHtml(string translatedText, List<HtmlPlaceholder> placeholders)
    {
        if (string.IsNullOrWhiteSpace(translatedText) || placeholders == null || placeholders.Count == 0)
            return translatedText ?? string.Empty;

        var result = translatedText;
        foreach (var placeholder in placeholders)
        {
            result = result.Replace(placeholder.Placeholder, placeholder.OriginalHtml);
        }

        return result;
    }

    /// <summary>
    /// Checks if the content appears to contain HTML
    /// </summary>
    public static bool ContainsHtml(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return false;

        return HtmlTagRegex().IsMatch(content);
    }

    /// <summary>
    /// Strips HTML tags but preserves the structure with placeholders
    /// Useful for translation APIs that don't support HTML
    /// </summary>
    public static (string CleanText, Dictionary<string, string> TagMap) StripHtmlForTranslation(string html)
    {
        if (!ContainsHtml(html))
            return (html, new Dictionary<string, string>());

        var tagMap = new Dictionary<string, string>();
        var processed = html;
        var tagCounter = 0;

        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        // Build a list of all tags in order
        var tagsToReplace = new List<(int Index, int Length, string Tag)>();
        CollectAllTags(doc.DocumentNode, processed, tagsToReplace);

        // Sort by index in descending order to replace from end to start
        tagsToReplace.Sort((a, b) => b.Index.CompareTo(a.Index));

        // Replace tags with placeholders from end to start
        foreach (var tag in tagsToReplace)
        {
            var tagId = $"__TAG{tagCounter}__";
            tagMap[tagId] = tag.Tag;
            processed = processed.Remove(tag.Index, tag.Length).Insert(tag.Index, tagId);
            tagCounter++;
        }

        // Decode entities
        processed = System.Net.WebUtility.HtmlDecode(processed);

        // Clean up whitespace
        processed = MultipleSpacesRegex().Replace(processed, " ");
        processed = processed.Trim();

        return (processed, tagMap);
    }

    private static void CollectAllTags(HtmlNode node, string originalHtml, List<(int Index, int Length, string Tag)> tags)
    {
        // For each element node, find its opening and closing tags in the original HTML
        foreach (var child in node.ChildNodes)
        {
            if (child.NodeType == HtmlNodeType.Element)
            {
                // Add opening tag
                if (child.StreamPosition >= 0 && child.StreamPosition < originalHtml.Length)
                {
                    // Find the end of opening tag
                    var startPos = child.StreamPosition;
                    var endPos = originalHtml.IndexOf('>', startPos);
                    if (endPos > startPos)
                    {
                        var tagLength = endPos - startPos + 1;
                        var tagText = originalHtml.Substring(startPos, tagLength);
                        tags.Add((startPos, tagLength, tagText));
                    }
                }

                // Recursively process children
                CollectAllTags(child, originalHtml, tags);

                // Add closing tag if it exists
                if (!child.Name.Equals("br", StringComparison.OrdinalIgnoreCase) &&
                    !child.Name.Equals("hr", StringComparison.OrdinalIgnoreCase) &&
                    !child.Name.Equals("img", StringComparison.OrdinalIgnoreCase) &&
                    !child.Name.Equals("input", StringComparison.OrdinalIgnoreCase) &&
                    !SelfClosingTags.Contains(child.Name) &&
                    !child.OuterHtml.TrimEnd().EndsWith("/>"))
                {
                    var closingTag = $"</{child.Name}>";
                    var lastIndex = originalHtml.LastIndexOf(closingTag,
                        child.StreamPosition + child.OuterHtml.Length > originalHtml.Length
                            ? originalHtml.Length - 1
                            : child.StreamPosition + child.OuterHtml.Length);

                    if (lastIndex >= 0)
                    {
                        tags.Add((lastIndex, closingTag.Length, closingTag));
                    }
                }
            }
        }
    }

    /// <summary>
    /// Restores HTML tags from placeholders after translation
    /// </summary>
    public static string RestoreHtmlAfterTranslation(string translatedText, Dictionary<string, string> tagMap)
    {
        if (string.IsNullOrWhiteSpace(translatedText) || tagMap == null || tagMap.Count == 0)
            return translatedText ?? string.Empty;

        var result = translatedText;
        foreach (var kvp in tagMap)
        {
            result = result.Replace(kvp.Key, kvp.Value);
        }

        return result;
    }

    // Regex patterns - keep simple tag detection that doesn't require full parsing
    [GeneratedRegex(@" {2,}")]
    private static partial Regex MultipleSpacesRegex();

    [GeneratedRegex(@"\n{3,}")]
    private static partial Regex MultipleNewlinesRegex();

    [GeneratedRegex(@"<(img|br|hr|input)[^>]*\/?>", RegexOptions.IgnoreCase)]
    private static partial Regex SelfClosingTagRegex();

    [GeneratedRegex(@"<[^>]+>")]
    private static partial Regex HtmlTagRegex();
}

/// <summary>
/// Represents an HTML element placeholder for text extraction
/// </summary>
public class HtmlPlaceholder
{
    public int Index { get; set; }
    public required string OriginalHtml { get; set; }
    public required string Placeholder { get; set; }
    public bool IsInline { get; set; }
}
