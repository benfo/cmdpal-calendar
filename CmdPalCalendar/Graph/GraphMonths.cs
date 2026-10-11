using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CmdPalCalendar.Events;
using CmdPalCalendar.Feeds;

namespace CmdPalCalendar.Graph;

internal sealed record MonthFailure(CalendarFeed Feed, DateOnly Month, Exception Error, DateTimeOffset? ShowingCopyFrom);

internal sealed class GraphMonths(IGraphEvents graph, TimeProvider time)
{
    private static readonly TimeSpan MaxAge = TimeSpan.FromMinutes(5);

    private readonly Lock _lock = new();
    private readonly Dictionary<MonthKey, Month> _months = [];

    public event EventHandler? Updated;

    public bool IsFetching
    {
        get
        {
            lock (_lock)
            {
                return _months.Values.Any(m => m.IsFetching);
            }
        }
    }

    public IReadOnlyList<MonthFailure> Failures
    {
        get
        {
            lock (_lock)
            {
                return _months
                    .Where(m => m.Value.Error is not null)
                    .Select(m => new MonthFailure(m.Value.Feed, m.Key.Month, m.Value.Error!, m.Value.LoadedAt))
                    .ToList();
            }
        }
    }

    public IEnumerable<CalendarEntry> GetEntries(CalendarFeed feed, DateTimeOffset from, DateTimeOffset to) =>
        MonthsBetween(from, to).SelectMany(month => Entries(feed, month)).ToList();

    public void Expire()
    {
        lock (_lock)
        {
            foreach (var key in _months.Keys.ToList())
            {
                _months[key] = _months[key] with { CheckedAt = DateTimeOffset.MinValue };
            }
        }
    }

    public void Retain(IReadOnlyList<CalendarFeed> feeds)
    {
        var keep = feeds.Select(f => (f.Id, f.Source)).ToHashSet();
        lock (_lock)
        {
            foreach (var key in _months.Keys.Where(k => !keep.Contains((k.FeedId, k.Source))).ToList())
            {
                _months.Remove(key);
            }
        }
    }

    private IReadOnlyList<CalendarEntry> Entries(CalendarFeed feed, DateOnly month)
    {
        var key = new MonthKey(feed.Id, feed.Source, month);
        lock (_lock)
        {
            var current = _months.GetValueOrDefault(key);
            if (current is not { IsFetching: true } && (current is null || time.GetUtcNow() - current.CheckedAt > MaxAge))
            {
                _months[key] = (current ?? new Month(feed, [], null, DateTimeOffset.MinValue, null, false)) with { IsFetching = true };
                _ = Task.Run(() => FetchAsync(feed, key));
            }

            return current?.Entries ?? [];
        }
    }

    private async Task FetchAsync(CalendarFeed feed, MonthKey key)
    {
        IReadOnlyList<CalendarEntry>? entries = null;
        Exception? error = null;
        try
        {
            entries = await graph.GetEntriesAsync(feed, LocalTime.StartOfDay(key.Month), LocalTime.StartOfDay(key.Month.AddMonths(1)), CancellationToken.None);
        }
        catch (Exception ex)
        {
            error = ex;
        }

        var now = time.GetUtcNow();
        lock (_lock)
        {
            if (!_months.TryGetValue(key, out var current))
            {
                return;
            }

            _months[key] = entries is null
                ? current with { CheckedAt = now, Error = error, IsFetching = false }
                : new Month(feed, entries, now, now, null, false);
        }

        Updated?.Invoke(this, EventArgs.Empty);
    }

    private static IEnumerable<DateOnly> MonthsBetween(DateTimeOffset from, DateTimeOffset to)
    {
        var first = FirstOfMonth(DateOnly.FromDateTime(from.ToLocalTime().DateTime));
        var last = DateOnly.FromDateTime(to.ToLocalTime().DateTime.AddTicks(-1));
        for (var month = first; month <= last; month = month.AddMonths(1))
        {
            yield return month;
        }
    }

    private static DateOnly FirstOfMonth(DateOnly date) => new(date.Year, date.Month, 1);

    private sealed record MonthKey(string FeedId, FeedSource Source, DateOnly Month);

    private sealed record Month(
        CalendarFeed Feed, IReadOnlyList<CalendarEntry> Entries, DateTimeOffset? LoadedAt, DateTimeOffset CheckedAt, Exception? Error, bool IsFetching);
}
