using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CmdPalCalendar.Events;
using CmdPalCalendar.Feeds;

namespace CmdPalCalendar.Ics;

internal sealed class IcsCalendarSource(
    Func<IReadOnlyList<CalendarFeed>> feeds,
    IcsFeedReader reader,
    IcsFeedCache cache,
    TimeProvider time)
    : ICalendarSource
{
    private static readonly TimeSpan MaxAge = TimeSpan.FromMinutes(5);

    private readonly Lock _lock = new();
    private IReadOnlyList<LoadedFeed> _loaded = [];
    private DateTimeOffset? _loadedAt;

    public bool IsStale =>
        _loadedAt is not { } loadedAt ||
        time.GetUtcNow() - loadedAt > MaxAge ||
        !Addresses(_loaded.Select(f => f.Feed)).SetEquals(Addresses(IcsCalendars()));

    public event EventHandler? Updated;

    public bool HasData => _loaded.Count > 0;

    public bool IsFetching => false;

    public IReadOnlyList<CalendarProblem> Problems =>
        Current()
            .Where(c => c.Loaded.Error is not null)
            .Select(c => new CalendarProblem(c.Feed.Name, c.Loaded.Error!, c.Loaded.CopyFrom))
            .ToList();

    public async Task LoadAsync(CancellationToken cancellationToken)
    {
        if (!HasData)
        {
            Replace(IcsCalendars().Select(f => FromCache(f) is { } copy ? new LoadedFeed(f, copy.Events, null, null) : null).OfType<LoadedFeed>().ToList());
        }

        var current = IcsCalendars();
        Replace(await Task.WhenAll(current.Select(f => LoadFeedAsync(f, cancellationToken))), loadedAt: time.GetUtcNow());
        cache.RemoveAllExcept(current);
    }

    public IReadOnlyList<CalendarEntry> GetEntries(DateOnly from, DateOnly toExclusive)
    {
        var rangeStart = LocalTime.StartOfDay(from);
        var rangeEnd = LocalTime.StartOfDay(toExclusive);

        lock (_lock)
        {
            return Current()
                .Where(c => c.Loaded.Events is not null)
                .SelectMany(c => c.Loaded.Events!.Overlapping(rangeStart, rangeEnd, c.Feed))
                .OrderBy(e => e.Start)
                .ThenBy(e => e.Title, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }
    }

    private List<CalendarFeed> IcsCalendars() => feeds().Where(f => f.Source is IcsSource).ToList();

    private IEnumerable<(CalendarFeed Feed, LoadedFeed Loaded)> Current()
    {
        var loaded = _loaded.ToDictionary(f => f.Feed.Id);
        return IcsCalendars()
            .Where(f => loaded.ContainsKey(f.Id) && loaded[f.Id].Feed.Source == f.Source)
            .Select(f => (f, loaded[f.Id]));
    }

    private async Task<LoadedFeed> LoadFeedAsync(CalendarFeed feed, CancellationToken cancellationToken)
    {
        try
        {
            var text = await reader.ReadAsync(feed.IcsLocation(), cancellationToken);
            var events = IcsEvents.From(IcsFeedReader.Parse(text));
            cache.Save(feed, text);
            return new LoadedFeed(feed, events, null, null);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            return FromCache(feed) is { } copy
                ? new LoadedFeed(feed, copy.Events, ex.Message, copy.SavedAt)
                : new LoadedFeed(feed, null, ex.Message, null);
        }
    }

    private (IcsEvents Events, DateTimeOffset SavedAt)? FromCache(CalendarFeed feed)
    {
        try
        {
            return cache.Load(feed) is { } copy ? (IcsEvents.From(IcsFeedReader.Parse(copy.Ics)), copy.SavedAt) : null;
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

    private static HashSet<(string Id, FeedSource Source)> Addresses(IEnumerable<CalendarFeed> feeds) =>
        feeds.Select(f => (f.Id, f.Source)).ToHashSet();

    private sealed record LoadedFeed(CalendarFeed Feed, IcsEvents? Events, string? Error, DateTimeOffset? CopyFrom);
}
