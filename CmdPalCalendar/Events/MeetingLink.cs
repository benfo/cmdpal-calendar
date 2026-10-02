using System;
using System.Text.RegularExpressions;

namespace CmdPalCalendar.Events;

internal enum MeetingService
{
    Other,
    Teams,
}

internal sealed partial record MeetingLink(MeetingService Service, Uri Web, Uri? App)
{
    public static MeetingLink? Parse(string? url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var web) || web.Scheme is not ("http" or "https"))
        {
            return null;
        }

        if (TeamsRegex().IsMatch(web.AbsoluteUri))
        {
            return new MeetingLink(MeetingService.Teams, web, WithScheme(web, "msteams"));
        }

        return new MeetingLink(MeetingService.Other, web, null);
    }

    private static Uri WithScheme(Uri web, string scheme) => new($"{scheme}://{web.Authority}{web.PathAndQuery}");

    [GeneratedRegex(@"^https://(((gov|dod)\.)?teams\.microsoft\.(com|us)|teams\.live\.com)/(l/meetup-join/|meet/\d+)", RegexOptions.IgnoreCase)]
    private static partial Regex TeamsRegex();
}
