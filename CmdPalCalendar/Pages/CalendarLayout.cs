using System;
using System.Collections.Generic;
using System.Linq;
using CmdPalCalendar.Events;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace CmdPalCalendar.Pages;

internal sealed class CalendarLayout(CalendarRows rows)
{
    public IListItem[] Build(CalendarContent content)
    {
        var days = content.Days
            .Select(d => d with { Entries = d.Entries.Where(e => e.Matches(content.Query)).ToList() })
            .ToList();

        IListItem[] body = days.All(d => d.Entries.Count == 0) ? Empty(content)
            : days.Count == 1 ? Day(days[0], content.Now)
            : Schedule(days, content.Now);

        IListItem[] goTo = content.GoTo is { } row ? [row] : [];
        return [.. goTo, .. body, .. new Section("Problems", content.Errors.Select(rows.Error).ToArray())];
    }

    private ListItem[] Empty(CalendarContent content) =>
        !string.IsNullOrWhiteSpace(content.Query) ? content.GoTo is null ? [rows.Message($"No events match '{content.Query.Trim()}'")] : []
        : [rows.Message($"Nothing on {DateText.Span(content.Days[0].Date, content.Days[^1].Date)}")];

    private IListItem[] Day(DayEntries day, DateTimeOffset now)
    {
        if (!IsToday(day.Date, now))
        {
            return [.. new Section("All day", Rows(day.Entries.Where(e => e.IsAllDay), null)), .. Rows(day.Entries.Where(e => !e.IsAllDay), null)];
        }

        var timed = day.Entries.Where(e => !e.IsAllDay).ToList();
        return
        [
            .. new Section("Happening now", Rows(timed.Where(e => e.Start <= now && e.End > now), now)),
            .. new Section("Up next", Rows(timed.Where(e => e.Start > now), now)),
            .. new Section("All day", Rows(day.Entries.Where(e => e.IsAllDay), now)),
            .. new Section("Earlier today", Rows(timed.Where(e => e.End <= now), now)),
        ];
    }

    private IListItem[] Schedule(IEnumerable<DayEntries> days, DateTimeOffset now) =>
        days.SelectMany(d => new Section(
                DateText.Title(d.Date, DateOnly.FromDateTime(now.DateTime)),
                Rows(d.Entries.OrderBy(e => !e.IsAllDay), IsToday(d.Date, now) ? now : null)))
            .ToArray();

    private ListItem[] Rows(IEnumerable<CalendarEntry> entries, DateTimeOffset? now) =>
        entries.Select(e => rows.Entry(e, now)).ToArray();

    private static bool IsToday(DateOnly date, DateTimeOffset now) => date == DateOnly.FromDateTime(now.DateTime);
}

internal sealed record DayEntries(DateOnly Date, IReadOnlyList<CalendarEntry> Entries);

internal sealed record CalendarContent(
    IReadOnlyList<DayEntries> Days,
    IReadOnlyList<string> Errors,
    DateTimeOffset Now,
    string Query,
    ListItem? GoTo);
