using System;
using System.Collections.Generic;
using System.Linq;
using CmdPalCalendar.Events;

namespace CmdPalCalendar.Pages;

internal static class EventText
{
    public static string Summary(CalendarEntry entry, DateOnly today)
    {
        var day = DateText.Short(DateOnly.FromDateTime(entry.Start.DateTime), today);
        var time = entry.IsAllDay ? "all day" : TimeText.Range(entry);
        IEnumerable<string?> parts = [$"{day}, {time}", entry.Title, entry.Location];
        return string.Join(" · ", parts.OfType<string>());
    }
}
