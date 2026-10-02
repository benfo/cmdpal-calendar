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
            : days.Count == 1 ? Day(days[0], content)
            : Schedule(days, content);

        IListItem[] goTo = content.GoTo is { } row ? [row] : [];
        return [.. goTo, .. body, .. new Section("Problems", content.Problems.Select(p => rows.Problem(p, content.Now)).ToArray())];
    }

    private ListItem[] Empty(CalendarContent content)
    {
        if (!content.IsFiltered)
        {
            return [PointToNext($"Nothing on {DateText.Span(content.Days[0].Date, content.Days[^1].Date)}", content)];
        }

        return content.GoTo is null ? [rows.Message($"No events match '{content.Query.Trim()}'")] : [];
    }

    private IListItem[] Day(DayEntries day, CalendarContent content)
    {
        var now = content.Now;
        if (!IsToday(day.Date, now))
        {
            return [.. new Section("All day", Rows(day.Entries.Where(e => e.IsAllDay), null, content)), .. Rows(day.Entries.Where(e => !e.IsAllDay), null, content)];
        }

        var timed = day.Entries.Where(e => !e.IsAllDay).ToList();
        IListItem[] finished = !content.IsFiltered && timed.All(e => e.End <= now)
            ? [PointToNext("That's it for today", content)]
            : [];

        return
        [
            .. finished,
            .. new Section("Happening now", Rows(timed.Where(e => e.Start <= now && e.End > now), now, content)),
            .. new Section("Up next", Rows(timed.Where(e => e.Start > now), now, content)),
            .. new Section("All day", Rows(day.Entries.Where(e => e.IsAllDay), now, content)),
            .. new Section("Earlier today", Rows(timed.Where(e => e.End <= now), now, content)),
        ];
    }

    private IListItem[] Schedule(IEnumerable<DayEntries> days, CalendarContent content) =>
        days.SelectMany(d => new Section(
                DateText.Title(d.Date, DateOnly.FromDateTime(content.Now.DateTime)),
                Rows(d.Entries.OrderBy(e => !e.IsAllDay), IsToday(d.Date, content.Now) ? content.Now : null, content)))
            .ToArray();

    private ListItem PointToNext(string title, CalendarContent content) =>
        rows.PointToNext(title, content.Next, DateOnly.FromDateTime(content.Now.DateTime), content.GoToDate);

    private ListItem[] Rows(IEnumerable<CalendarEntry> entries, DateTimeOffset? now, CalendarContent content) =>
        entries.Select(e => rows.Entry(e, now, content.ShowCalendars)).ToArray();

    private static bool IsToday(DateOnly date, DateTimeOffset now) => date == DateOnly.FromDateTime(now.DateTime);
}

internal sealed record DayEntries(DateOnly Date, IReadOnlyList<CalendarEntry> Entries);

internal sealed record CalendarContent(
    IReadOnlyList<DayEntries> Days,
    IReadOnlyList<CalendarProblem> Problems,
    DateTimeOffset Now,
    string Query,
    ListItem? GoTo,
    CalendarEntry? Next,
    Action<DateOnly> GoToDate,
    bool ShowCalendars)
{
    public bool IsFiltered => !string.IsNullOrWhiteSpace(Query);
}
