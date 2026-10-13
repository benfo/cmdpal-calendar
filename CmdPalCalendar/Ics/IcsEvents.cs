using System;
using System.Collections.Generic;
using System.Linq;
using CmdPalCalendar.Events;
using CmdPalCalendar.Feeds;
using Ical.Net;
using Ical.Net.CalendarComponents;
using Ical.Net.DataTypes;

namespace CmdPalCalendar.Ics;

// Ical.Net's object model takes ~25 times the feed's size, so only recurring events stay in it.
internal sealed class IcsEvents
{
    private static readonly TimeSpan LongestRecurringEvent = TimeSpan.FromDays(14);

    private readonly Calendar _recurring;
    private readonly IReadOnlyList<SingleEvent> _single;

    private IcsEvents(Calendar recurring, IReadOnlyList<SingleEvent> single)
    {
        _recurring = recurring;
        _single = single;
    }

    public static IcsEvents From(Calendar calendar)
    {
        var events = calendar.Events.ToList();
        var recurringUids = events.Where(IsRecurring).Select(e => e.Uid).ToHashSet();
        var single = events.Where(e => !recurringUids.Contains(e.Uid)).ToList();

        foreach (var ev in single)
        {
            calendar.Children.Remove(ev);
        }

        return new IcsEvents(
            calendar,
            single
                .Where(e => !IsCancelled(e))
                .Select(e => e.GetOccurrences().FirstOrDefault() is { } o ? new SingleEvent(IcsEventDetails.From(e), o.Period.StartTime, o.Period.EffectiveEndTime ?? o.Period.StartTime) : null)
                .OfType<SingleEvent>()
                .ToList());
    }

    public IEnumerable<CalendarEntry> Overlapping(DateTimeOffset start, DateTimeOffset end, CalendarFeed feed)
    {
        var recurring = _recurring.GetOccurrences<CalendarEvent>(new CalDateTime(start.Subtract(LongestRecurringEvent).UtcDateTime))
            .TakeWhileBefore(new CalDateTime(end.UtcDateTime))
            .Where(o => o.Source is CalendarEvent ev && !IsCancelled(ev))
            .Select(o => IcsEntryMapper.ToEntry((CalendarEvent)o.Source, o.Period, feed));

        var single = _single
            .Where(e => IcsEntryMapper.ToLocal(e.Start) < end && IcsEntryMapper.ToLocal(e.End) > start)
            .Select(e => IcsEntryMapper.ToEntry(e.Details, e.Start, e.End, feed));

        return recurring.Concat(single)
            .Where(e => e.Start < end && e.End > start)
            .DistinctBy(e => (e.Uid, e.Start));
    }

    private static bool IsRecurring(CalendarEvent ev) =>
        ev.Properties.ContainsKey("RRULE") || ev.Properties.ContainsKey("RDATE") || ev.Properties.ContainsKey("RECURRENCE-ID");

    private static bool IsCancelled(CalendarEvent ev) =>
        string.Equals(ev.Status, "CANCELLED", StringComparison.OrdinalIgnoreCase);

    private sealed record SingleEvent(IcsEventDetails Details, CalDateTime Start, CalDateTime End);
}
