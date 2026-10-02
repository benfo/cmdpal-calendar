using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace CmdPalCalendar.Events;

internal enum MeetingService
{
    Other,
    Teams,
    Zoom,
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

        if (ZoomHostRegex().IsMatch(web.Host))
        {
            return new MeetingLink(MeetingService.Zoom, web, ZoomApp(web));
        }

        return new MeetingLink(MeetingService.Other, web, null);
    }

    private static Uri WithScheme(Uri web, string scheme) => new($"{scheme}://{web.Authority}{web.PathAndQuery}");

    private static Uri? ZoomApp(Uri web)
    {
        var meeting = ZoomMeetingPathRegex().Match(web.AbsolutePath);
        if (!meeting.Success)
        {
            return null;
        }

        var extra = web.Query.TrimStart('?')
            .Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Where(p => !p.StartsWith("tk=", StringComparison.OrdinalIgnoreCase));

        return new Uri($"zoommtg://{web.Host}/join?{string.Join('&', ["action=join", $"confno={meeting.Groups["id"].Value}", .. extra])}");
    }

    [GeneratedRegex(@"^https://(((gov|dod)\.)?teams\.microsoft\.(com|us)|teams\.live\.com)/(l/meetup-join/|meet/\d+)", RegexOptions.IgnoreCase)]
    private static partial Regex TeamsRegex();

    [GeneratedRegex(@"^([\w-]+\.)?zoom(gov)?\.(us|com)$", RegexOptions.IgnoreCase)]
    private static partial Regex ZoomHostRegex();

    [GeneratedRegex(@"^/(j|wc/join)/(?<id>\d{9,11})/?$")]
    private static partial Regex ZoomMeetingPathRegex();
}
