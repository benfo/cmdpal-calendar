using System;
using System.Collections.Generic;
using System.Linq;
using CmdPalCalendar.Events;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace CmdPalCalendar.Pages;

internal sealed class CalendarLayout(CalendarRows rows)
{
    public IListItem[] Day(DateOnly date, IReadOnlyList<CalendarEntry> entries, IReadOnlyList<string> errors, DateTimeOffset now)
    {
        var isToday = date == DateOnly.FromDateTime(now.DateTime);
        IListItem[] body = isToday ? Today(entries, now) : OtherDay(entries);

        if (entries.Count == 0)
        {
            body = [rows.Message($"Nothing on {DateText.Long(date)}")];
        }

        return [.. body, .. new Section("Problems", errors.Select(rows.Error).ToArray())];
    }

    private IListItem[] Today(IReadOnlyList<CalendarEntry> entries, DateTimeOffset now)
    {
        var timed = entries.Where(e => !e.IsAllDay).ToList();
        ListItem[] Rows(IEnumerable<CalendarEntry> selected) => selected.Select(e => rows.Entry(e, now)).ToArray();

        return
        [
            .. new Section("Happening now", Rows(timed.Where(e => e.Start <= now && e.End > now))),
            .. new Section("Up next", Rows(timed.Where(e => e.Start > now))),
            .. new Section("All day", Rows(entries.Where(e => e.IsAllDay))),
            .. new Section("Earlier today", Rows(timed.Where(e => e.End <= now))),
        ];
    }

    private IListItem[] OtherDay(IReadOnlyList<CalendarEntry> entries) =>
    [
        .. new Section("All day", entries.Where(e => e.IsAllDay).Select(e => rows.Entry(e, now: null)).ToArray()),
        .. entries.Where(e => !e.IsAllDay).Select(e => rows.Entry(e, now: null)),
    ];
}
