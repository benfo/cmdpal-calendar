using System;
using System.Collections.Generic;
using System.Linq;

namespace CmdPalCalendar.Events;

internal enum MeetingPhase
{
    Upcoming,
    Happening,
    Ended,
}

internal static class MeetingPhases
{
    public static MeetingPhase At(CalendarEntry entry, DateTimeOffset now) =>
        now < entry.Start ? MeetingPhase.Upcoming
        : now < entry.End ? MeetingPhase.Happening
        : MeetingPhase.Ended;

    public static bool ChangedBetween(IEnumerable<CalendarEntry> entries, DateTimeOffset before, DateTimeOffset now) =>
        DateOnly.FromDateTime(before.DateTime) != DateOnly.FromDateTime(now.DateTime) ||
        entries.Any(e => At(e, before) != At(e, now));
}
