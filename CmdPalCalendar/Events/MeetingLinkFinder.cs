using System.Linq;
using System.Text.RegularExpressions;

namespace CmdPalCalendar.Events;

internal static partial class MeetingLinkFinder
{
    public static string? Find(params string?[] texts)
    {
        var urls = texts
            .Where(t => !string.IsNullOrEmpty(t))
            .SelectMany(t => UrlRegex().Matches(t!).Select(m => LinkUnwrapper.Unwrap(m.Value.TrimEnd('.', ',', ';', ')', '>'))))
            .ToList();

        return urls.FirstOrDefault(u => MeetingHostRegex().IsMatch(u)) ?? urls.FirstOrDefault();
    }

    [GeneratedRegex(@"https?://[^\s""'<>]+", RegexOptions.IgnoreCase)]
    private static partial Regex UrlRegex();

    [GeneratedRegex(@"^https?://([\w-]+\.)*(teams\.microsoft\.com|teams\.live\.com|zoom\.us|zoom\.com|zoomgov\.com|meet\.google\.com|webex\.com)/", RegexOptions.IgnoreCase)]
    private static partial Regex MeetingHostRegex();
}
