using System;
using System.Linq;
using CmdPalCalendar.Events;
using Ical.Net.CalendarComponents;
using Ical.Net.DataTypes;

namespace CmdPalCalendar.Ics;

internal static class IcsEntryMapper
{
    public static CalendarEntry ToEntry(CalendarEvent ev, Period period, string source)
    {
        var start = period.StartTime;
        var end = period.EffectiveEndTime ?? start;

        return new CalendarEntry(
            Uid: ev.Uid ?? Guid.NewGuid().ToString(),
            Title: NullIfBlank(ev.Summary) ?? "(No title)",
            Start: ToLocal(start),
            End: ToLocal(end),
            IsAllDay: !start.HasTime,
            Location: NullIfBlank(ev.Location),
            Description: NullIfBlank(ev.Description),
            Organizer: ev.Organizer is { } organizer ? NameOrAddress(organizer.CommonName, organizer.Value) : null,
            Attendees: ev.Attendees.Select(a => NameOrAddress(a.CommonName, a.Value)).OfType<string>().ToList(),
            Link: MeetingLinkFinder.Find(ev.Location, ev.Url?.ToString(), ev.Description),
            Source: source,
            Notes: EventNotes.From(ev.Description, ev.Properties.Get<string>("X-ALT-DESC")));
    }

    private static DateTimeOffset ToLocal(CalDateTime value)
    {
        if (value.IsFloating || !value.HasTime)
        {
            var wallClock = DateTime.SpecifyKind(value.Value, DateTimeKind.Unspecified);
            return new DateTimeOffset(wallClock, TimeZoneInfo.Local.GetUtcOffset(wallClock));
        }

        return new DateTimeOffset(value.AsUtc).ToLocalTime();
    }

    private static string? NameOrAddress(string? name, Uri? address) =>
        NullIfBlank(name) ?? NullIfBlank(address?.OriginalString.Replace("mailto:", string.Empty, StringComparison.OrdinalIgnoreCase));

    private static string? NullIfBlank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
