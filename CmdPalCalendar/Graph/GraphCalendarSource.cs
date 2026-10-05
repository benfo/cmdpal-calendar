using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CmdPalCalendar.Events;
using CmdPalCalendar.Feeds;

namespace CmdPalCalendar.Graph;

internal sealed class GraphCalendarSource(Func<IReadOnlyList<CalendarFeed>> feeds, IGraphEvents graph, TimeProvider time) : ICalendarSource
{
    private const int DaysBack = 14;
    private const int DaysAhead = 90;
    private static readonly TimeSpan MaxAge = TimeSpan.FromMinutes(5);

    private readonly Lock _lock = new();
    private IReadOnlyDictionary<string, LoadedFeed> _loaded = new Dictionary<string, LoadedFeed>();
    private DateTimeOffset? _loadedAt;

    public event EventHandler? Updated;

    public bool IsStale =>
        _loadedAt is not { } loadedAt ||
        time.GetUtcNow() - loadedAt > MaxAge ||
        !Keys(_loaded.Values.Select(l => l.Feed)).SetEquals(Keys(MicrosoftFeeds()));

    public bool HasData => _loaded.Values.Any(l => l.Entries is not null);

    public IReadOnlyList<CalendarProblem> Problems =>
        Current()
            .Where(c => c.Loaded.Error is not null)
            .Select(c => new CalendarProblem(c.Feed.Name, c.Loaded.Error!, c.Loaded.Entries is null ? null : c.Loaded.LoadedAt))
            .ToList();

    public async Task LoadAsync(CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(time.GetLocalNow().DateTime);
        var from = LocalTime.StartOfDay(today.AddDays(-DaysBack));
        var to = LocalTime.StartOfDay(today.AddDays(DaysAhead + 1));

        var loaded = await Task.WhenAll(MicrosoftFeeds().Select(f => LoadFeedAsync(f, from, to, cancellationToken)));
        lock (_lock)
        {
            _loaded = loaded.ToDictionary(l => l.Feed.Id);
            _loadedAt = time.GetUtcNow();
        }

        Updated?.Invoke(this, EventArgs.Empty);
    }

    public IReadOnlyList<CalendarEntry> GetEntries(DateOnly from, DateOnly toExclusive)
    {
        var rangeStart = LocalTime.StartOfDay(from);
        var rangeEnd = LocalTime.StartOfDay(toExclusive);

        return Current()
            .SelectMany(c => (c.Loaded.Entries ?? []).Select(e => e with { Source = c.Feed.Name, Color = c.Feed.Color }))
            .Where(e => e.Start < rangeEnd && e.End > rangeStart)
            .OrderBy(e => e.Start)
            .ThenBy(e => e.Title, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    private async Task<LoadedFeed> LoadFeedAsync(CalendarFeed feed, DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken)
    {
        try
        {
            return new LoadedFeed(feed, await graph.GetEntriesAsync(feed, from, to, cancellationToken), null, time.GetUtcNow());
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            return _loaded.GetValueOrDefault(feed.Id) is { Entries: not null } previous && previous.Feed.Source == feed.Source
                ? previous with { Feed = feed, Error = GraphErrors.Describe(ex) }
                : new LoadedFeed(feed, null, GraphErrors.Describe(ex), null);
        }
    }

    private List<CalendarFeed> MicrosoftFeeds() => feeds().Where(f => f.Source is MicrosoftSource).ToList();

    private IEnumerable<(CalendarFeed Feed, LoadedFeed Loaded)> Current()
    {
        var loaded = _loaded;
        return MicrosoftFeeds()
            .Where(f => loaded.TryGetValue(f.Id, out var l) && l.Feed.Source == f.Source)
            .Select(f => (f, loaded[f.Id]));
    }

    private static HashSet<(string Id, FeedSource Source)> Keys(IEnumerable<CalendarFeed> feeds) =>
        feeds.Select(f => (f.Id, f.Source)).ToHashSet();

    private sealed record LoadedFeed(CalendarFeed Feed, IReadOnlyList<CalendarEntry>? Entries, string? Error, DateTimeOffset? LoadedAt);
}
