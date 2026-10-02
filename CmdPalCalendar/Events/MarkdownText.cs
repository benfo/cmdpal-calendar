using System.Linq;

namespace CmdPalCalendar.Events;

internal static class MarkdownText
{
    private const string Specials = "\\`*_{}[]<>()#+-!|";

    public static string Escape(string text) =>
        string.Concat(text.Select(c => Specials.Contains(c) ? $"\\{c}" : c.ToString()));
}
