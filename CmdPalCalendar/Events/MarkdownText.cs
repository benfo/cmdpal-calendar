using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace CmdPalCalendar.Events;

internal static partial class MarkdownText
{
    private const string Specials = "\\`*_{}[]<>()#+-!|";

    public static string Escape(string text) =>
        string.Concat(text.Select(c => Specials.Contains(c) ? $"\\{c}" : c.ToString()));

    public static string FromPlainText(string text) => Tidy(Escape(text));

    public static string Tidy(string markdown) => Join(Paragraphs(markdown));

    public static string Join(IEnumerable<IReadOnlyList<string>> paragraphs) =>
        string.Join("\n\n", paragraphs.Select(p => string.Join("  \n", p)));

    public static IReadOnlyList<IReadOnlyList<string>> Paragraphs(string markdown)
    {
        var paragraphs = new List<List<string>> { new() };
        foreach (var line in markdown.Replace("\r", string.Empty, StringComparison.Ordinal).Split('\n').Select(l => SpacesRegex().Replace(l, " ").Trim()))
        {
            if (line.Length == 0)
            {
                paragraphs.Add([]);
            }
            else
            {
                paragraphs[^1].Add(line);
            }
        }

        return paragraphs.Where(p => p.Count > 0).ToList();
    }

    [GeneratedRegex(@"[ \t\u00A0]+")]
    private static partial Regex SpacesRegex();
}
