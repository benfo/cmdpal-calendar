using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace CmdPalCalendar.Events;

internal static partial class EventNotes
{
    public static string? From(string? description, string? html)
    {
        var markdown = !string.IsNullOrWhiteSpace(html) ? HtmlToMarkdown.Convert(html)
            : string.IsNullOrWhiteSpace(description) ? null
            : HtmlToMarkdown.LooksLikeHtml(description) ? HtmlToMarkdown.Convert(description)
            : MarkdownText.FromPlainText(description);

        return markdown is null ? null : WithoutInviteBlock(markdown);
    }

    private static string? WithoutInviteBlock(string markdown)
    {
        var kept = new List<IReadOnlyList<string>>();
        foreach (var paragraph in MarkdownText.Paragraphs(markdown))
        {
            var lines = paragraph.TakeWhile(line => !InviteStartRegex().IsMatch(line)).ToList();
            if (lines.Count > 0)
            {
                kept.Add(lines);
            }

            if (lines.Count < paragraph.Count)
            {
                break;
            }
        }

        return kept.Count == 0 ? null : MarkdownText.Join(kept);
    }

    [GeneratedRegex(@"^((\\?_){10,}|Join Zoom Meeting|Microsoft Teams (meeting|Need help\?))$", RegexOptions.IgnoreCase)]
    private static partial Regex InviteStartRegex();
}
