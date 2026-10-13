using System;
using CmdPalCalendar.Events;
using CmdPalCalendar.Feeds;
using Ical.Net.CalendarComponents;
using Ical.Net.DataTypes;

namespace CmdPalCalendar.Ics;

internal static class IcsEntryMapper
{
    public static CalendarEntry ToEntry(CalendarEvent ev, Period period, CalendarFeed feed) =>
        ToEntry(IcsEventDetails.From(ev), period.StartTime, period.EffectiveEndTime ?? period.StartTime, feed);

    public static CalendarEntry ToEntry(IcsEventDetails ev, CalDateTime start, CalDateTime end, CalendarFeed feed) =>
        new(
            Uid: ev.Uid ?? Guid.NewGuid().ToString(),
            Title: IcsText.NullIfBlank(ev.Summary) ?? "(No title)",
            Start: ToLocal(start),
            End: ToLocal(end),
            IsAllDay: !start.HasTime,
            Location: IcsText.NullIfBlank(ev.Location),
            Description: IcsText.NullIfBlank(ev.Description),
            Organizer: ev.Organizer,
            Attendees: ev.Attendees,
            Link: MeetingLinkFinder.Find(ev.Location, ev.Url, ev.Description),
            Source: feed.Name,
            Notes: EventNotes.From(ev.Description, ev.HtmlDescription),
            Color: feed.Color,
            Site: CalendarSites.Detect(feed.IcsLocation()));

    public static DateTimeOffset ToLocal(CalDateTime value)
    {
        if (value.IsFloating || !value.HasTime)
        {
            return LocalTime.FromWallClock(value.Value);
        }

        return new DateTimeOffset(value.AsUtc).ToLocalTime();
    }
}
