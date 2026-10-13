using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
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
    private volatile bool _parsed;

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
            Replace(IcsCalendars().Select(f => FromCache(f) is { } copy ? new LoadedFeed(f, copy.Events, copy.Hash, null, null) : null).OfType<LoadedFeed>().ToList());
        }

        var current = IcsCalendars();
        var previous = _loaded.ToDictionary(f => f.Feed.Id);
        Replace(await Task.WhenAll(current.Select(f => LoadFeedAsync(f, previous.GetValueOrDefault(f.Id), cancellationToken))), loadedAt: time.GetUtcNow());
        cache.RemoveAllExcept(current);
        ReleaseParsingMemory();
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

    private async Task<LoadedFeed> LoadFeedAsync(CalendarFeed feed, LoadedFeed? previous, CancellationToken cancellationToken)
    {
        try
        {
            var text = await reader.ReadAsync(feed.IcsLocation(), cancellationToken);
            var hash = Hash(text);
            var events = previous is { Events: { } unchanged } && previous.Hash == hash
                ? unchanged
                : Parse(text);
            cache.Save(feed, text);
            return new LoadedFeed(feed, events, hash, null, null);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            return FromCache(feed) is { } copy
                ? new LoadedFeed(feed, copy.Events, copy.Hash, ex.Message, copy.SavedAt)
                : new LoadedFeed(feed, null, null, ex.Message, null);
        }
    }

    private (IcsEvents Events, string Hash, DateTimeOffset SavedAt)? FromCache(CalendarFeed feed)
    {
        try
        {
            return cache.Load(feed) is { } copy ? (Parse(copy.Ics), Hash(copy.Ics), copy.SavedAt) : null;
        }
        catch (InvalidDataException)
        {
            return null;
        }
    }

    private IcsEvents Parse(string text)
    {
        _parsed = true;
        return IcsEvents.From(IcsFeedReader.Parse(text));
    }

    // Parsing a large feed briefly takes hundreds of MB; without this the process keeps it for minutes.
    private void ReleaseParsingMemory()
    {
        if (_parsed)
        {
            _parsed = false;
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, blocking: true, compacting: true);
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

    private static string Hash(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));

    private static HashSet<(string Id, FeedSource Source)> Addresses(IEnumerable<CalendarFeed> feeds) =>
        feeds.Select(f => (f.Id, f.Source)).ToHashSet();

    private sealed record LoadedFeed(CalendarFeed Feed, IcsEvents? Events, string? Hash, string? Error, DateTimeOffset? CopyFrom);
}
