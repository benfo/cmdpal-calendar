using System;
using System.Collections.Generic;
using System.Linq;
using CmdPalCalendar.Events;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace CmdPalCalendar.Pages;

internal sealed class CalendarLayout(CalendarRows rows)
{
    public IListItem[] Day(DayContent content)
    {
        var entries = content.Entries.Where(e => e.Matches(content.Query)).ToList();
        var isToday = content.Date == DateOnly.FromDateTime(content.Now.DateTime);

        IListItem[] body = entries.Count > 0
            ? isToday ? Today(entries, content.Now) : OtherDay(entries)
            : Empty(content);

        IListItem[] goTo = content.GoTo is { } row ? [row] : [];
        return [.. goTo, .. body, .. new Section("Problems", content.Errors.Select(rows.Error).ToArray())];
    }

    private ListItem[] Empty(DayContent content) =>
        string.IsNullOrWhiteSpace(content.Query) ? [rows.Message($"Nothing on {DateText.Long(content.Date)}")]
        : content.GoTo is null ? [rows.Message($"No events match '{content.Query.Trim()}'")]
        : [];

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

internal sealed record DayContent(
    DateOnly Date,
    IReadOnlyList<CalendarEntry> Entries,
    IReadOnlyList<string> Errors,
    DateTimeOffset Now,
    string Query,
    ListItem? GoTo);
