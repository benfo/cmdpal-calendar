using System;
using System.Collections.Generic;
using System.Linq;

namespace CmdPalCalendar.Events;

internal static class UpcomingMeeting
{
    public static readonly TimeSpan StartingSoon = TimeSpan.FromMinutes(5);

    public static readonly TimeSpan JoinWindow = TimeSpan.FromMinutes(15);

    public static CalendarEntry? Find(IEnumerable<CalendarEntry> entries, DateTimeOffset now)
    {
        var remaining = entries.Where(e => !e.IsAllDay && e.End > now).OrderBy(e => e.Start).ToList();

        return remaining.FirstOrDefault(e => e.Start > now && e.Start - now <= StartingSoon)
            ?? remaining.Where(e => e.Start <= now).MaxBy(e => e.Start)
            ?? remaining.FirstOrDefault();
    }

    public static CalendarEntry? FindJoinable(IEnumerable<CalendarEntry> entries, DateTimeOffset now) =>
        Find(entries.Where(e => MeetingLink.Parse(e.Link) is { Service: not MeetingService.Other }), now);

    public static bool IsJoinableNow(CalendarEntry meeting, DateTimeOffset now) => meeting.Start - now <= JoinWindow;
}
