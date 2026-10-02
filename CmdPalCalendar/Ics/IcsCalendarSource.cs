using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CmdPalCalendar.Events;
using CmdPalCalendar.Feeds;
using Ical.Net;
using Ical.Net.CalendarComponents;
using Ical.Net.DataTypes;

namespace CmdPalCalendar.Ics;

internal sealed class IcsCalendarSource(Func<IReadOnlyList<CalendarFeed>> feeds, IcsFeedReader reader, TimeProvider time)
    : ICalendarSource
{
    private static readonly TimeSpan MaxAge = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan LongestEvent = TimeSpan.FromDays(14);

    private readonly Lock _lock = new();
    private IReadOnlyList<LoadedFeed> _loaded = [];
    private DateTimeOffset? _loadedAt;

    public bool IsStale =>
        _loadedAt is not { } loadedAt ||
        time.GetUtcNow() - loadedAt > MaxAge ||
        !Addresses(_loaded.Select(f => f.Feed)).SetEquals(Addresses(feeds()));

    public IReadOnlyList<string> Errors =>
        Current().Select(c => c.Loaded.Error is { } error ? $"{c.Feed.Name}: {error}" : null).OfType<string>().ToList();

    public async Task LoadAsync(CancellationToken cancellationToken)
    {
        var loaded = await Task.WhenAll(feeds().Select(f => LoadFeedAsync(f, cancellationToken)));

        lock (_lock)
        {
            _loaded = loaded;
            _loadedAt = time.GetUtcNow();
        }
    }

    public IReadOnlyList<CalendarEntry> GetEntries(DateOnly from, DateOnly toExclusive)
    {
        var rangeStart = StartOfDay(from);
        var rangeEnd = StartOfDay(toExclusive);
        var expandFrom = new CalDateTime(rangeStart.Subtract(LongestEvent).UtcDateTime);
        var expandTo = new CalDateTime(rangeEnd.UtcDateTime);

        lock (_lock)
        {
            return Current()
                .Where(c => c.Loaded.Calendar is not null)
                .SelectMany(c => c.Loaded.Calendar!.GetOccurrences<CalendarEvent>(expandFrom)
                    .TakeWhileBefore(expandTo)
                    .Where(o => o.Source is CalendarEvent ev && !IsCancelled(ev))
                    .Select(o => IcsEntryMapper.ToEntry((CalendarEvent)o.Source, o.Period, c.Feed.Name)))
                .Where(e => e.Start < rangeEnd && e.End > rangeStart)
                .DistinctBy(e => (e.Uid, e.Start))
                .OrderBy(e => e.Start)
                .ThenBy(e => e.Title, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }
    }

    private IEnumerable<(CalendarFeed Feed, LoadedFeed Loaded)> Current()
    {
        var loaded = _loaded.ToDictionary(f => f.Feed.Id);
        return feeds()
            .Where(f => loaded.ContainsKey(f.Id) && loaded[f.Id].Feed.Location == f.Location)
            .Select(f => (f, loaded[f.Id]));
    }

    private async Task<LoadedFeed> LoadFeedAsync(CalendarFeed feed, CancellationToken cancellationToken)
    {
        try
        {
            return new LoadedFeed(feed, await reader.ReadCalendarAsync(feed.Location, cancellationToken), null);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            return new LoadedFeed(feed, null, ex.Message);
        }
    }

    private static HashSet<(string Id, string Location)> Addresses(IEnumerable<CalendarFeed> feeds) =>
        feeds.Select(f => (f.Id, f.Location)).ToHashSet();

    private static DateTimeOffset StartOfDay(DateOnly date)
    {
        var midnight = date.ToDateTime(TimeOnly.MinValue);
        return new DateTimeOffset(midnight, TimeZoneInfo.Local.GetUtcOffset(midnight));
    }

    private static bool IsCancelled(CalendarEvent ev) =>
        string.Equals(ev.Status, "CANCELLED", StringComparison.OrdinalIgnoreCase);

    private sealed record LoadedFeed(CalendarFeed Feed, Calendar? Calendar, string? Error);
}
