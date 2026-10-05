using System;
using System.Globalization;
using System.Linq;
using CmdPalCalendar.Events;
using CmdPalCalendar.Feeds;
using Microsoft.Graph.Models;

namespace CmdPalCalendar.Graph;

internal static class GraphEntryMapper
{
    public static CalendarEntry ToEntry(Event ev, CalendarFeed feed)
    {
        var isAllDay = ev.IsAllDay ?? false;
        var html = ev.Body?.ContentType == BodyType.Html ? ev.Body.Content : null;
        var text = ev.Body?.ContentType == BodyType.Text ? ev.Body.Content : null;
        var description = html is not null ? HtmlToMarkdown.Convert(html) : NullIfBlank(text);
        var location = NullIfBlank(ev.Location?.DisplayName);
        var webLink = Uri.TryCreate(ev.WebLink, UriKind.Absolute, out var web) ? web : null;
        var start = ToLocal(ev.Start, isAllDay);

        return new CalendarEntry(
            Uid: ev.Id ?? Guid.NewGuid().ToString(),
            Title: NullIfBlank(ev.Subject) ?? "(No title)",
            Start: start,
            End: ev.End is null ? start : ToLocal(ev.End, isAllDay),
            IsAllDay: isAllDay,
            Location: location,
            Description: description,
            Organizer: NameOrAddress(ev.Organizer?.EmailAddress),
            Attendees: (ev.Attendees ?? []).Select(a => NameOrAddress(a.EmailAddress)).OfType<string>().ToList(),
            Link: NullIfBlank(ev.OnlineMeeting?.JoinUrl) ?? MeetingLinkFinder.Find(location, description),
            Source: feed.Name,
            Notes: EventNotes.From(text, html),
            Color: feed.Color,
            Site: webLink is null ? null : CalendarSites.Detect(webLink.AbsoluteUri),
            WebLink: webLink);
    }

    private static DateTimeOffset ToLocal(DateTimeTimeZone? value, bool isAllDay)
    {
        var parsed = DateTime.Parse(value?.DateTime ?? string.Empty, CultureInfo.InvariantCulture, DateTimeStyles.None);
        if (isAllDay)
        {
            var wallClock = DateTime.SpecifyKind(parsed.Date, DateTimeKind.Unspecified);
            return new DateTimeOffset(wallClock, TimeZoneInfo.Local.GetUtcOffset(wallClock));
        }

        return new DateTimeOffset(DateTime.SpecifyKind(parsed, DateTimeKind.Utc)).ToLocalTime();
    }

    private static string? NameOrAddress(EmailAddress? email) => NullIfBlank(email?.Name) ?? NullIfBlank(email?.Address);

    private static string? NullIfBlank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
