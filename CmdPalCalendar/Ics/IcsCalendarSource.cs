using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CmdPalCalendar.Events;
using CmdPalCalendar.Feeds;
using Ical.Net;
using Ical.Net.CalendarComponents;
using Ical.Net.DataTypes;

namespace CmdPalCalendar.Ics;

internal sealed class IcsCalendarSource(
    Func<IReadOnlyList<CalendarFeed>> feeds,
    IcsFeedReader reader,
    IcsFeedCache cache,
    TimeProvider time)
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

    public event EventHandler? Updated;

    public bool HasData => _loaded.Count > 0;

    public IReadOnlyList<CalendarProblem> Problems =>
        Current()
            .Where(c => c.Loaded.Error is not null)
            .Select(c => new CalendarProblem(c.Feed.Name, c.Loaded.Error!, c.Loaded.CopyFrom))
            .ToList();

    public async Task LoadAsync(CancellationToken cancellationToken)
    {
        if (!HasData)
        {
            Replace(feeds().Select(f => FromCache(f) is { } copy ? new LoadedFeed(f, copy.Calendar, null, null) : null).OfType<LoadedFeed>().ToList());
        }

        var current = feeds();
        Replace(await Task.WhenAll(current.Select(f => LoadFeedAsync(f, cancellationToken))), loadedAt: time.GetUtcNow());
        cache.RemoveAllExcept(current);
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
                    .Select(o => IcsEntryMapper.ToEntry((CalendarEvent)o.Source, o.Period, c.Feed)))
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
            var text = await reader.ReadAsync(feed.Location, cancellationToken);
            var calendar = IcsFeedReader.Parse(text);
            cache.Save(feed, text);
            return new LoadedFeed(feed, calendar, null, null);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            return FromCache(feed) is { } copy
                ? new LoadedFeed(feed, copy.Calendar, ex.Message, copy.SavedAt)
                : new LoadedFeed(feed, null, ex.Message, null);
        }
    }

    private (Calendar Calendar, DateTimeOffset SavedAt)? FromCache(CalendarFeed feed)
    {
        try
        {
            return cache.Load(feed) is { } copy ? (IcsFeedReader.Parse(copy.Ics), copy.SavedAt) : null;
        }
        catch (InvalidDataException)
        {
            return null;
        }
    }

    private void Replace(IReadOnlyList<LoadedFeed> loaded, DateTimeOffset? loadedAt = null)
    {
        lock (_lock)
        {
            _loaded = loaded;
            _loadedAt = loadedAt ?? _loadedAt;
        }

        Updated?.Invoke(this, EventArgs.Empty);
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

    private sealed record LoadedFeed(CalendarFeed Feed, Calendar? Calendar, string? Error, DateTimeOffset? CopyFrom);
}
