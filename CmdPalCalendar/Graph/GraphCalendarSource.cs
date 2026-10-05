using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CmdPalCalendar.Events;
using CmdPalCalendar.Feeds;

namespace CmdPalCalendar.Graph;

internal sealed class GraphCalendarSource(
    Func<IReadOnlyList<CalendarFeed>> feeds,
    IGraphEvents graph,
    GraphEventCache cache,
    TimeProvider time)
    : ICalendarSource
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

    public bool HasData => _loaded.Values.Any(l => l.Copy is not null);

    public IReadOnlyList<CalendarProblem> Problems
    {
        get
        {
            var failed = Current().Select(c => c.Loaded).Where(l => l.Error is not null).ToList();
            var signIns = failed
                .Where(l => l.SignIn is not null)
                .GroupBy(l => l.SignIn!)
                .Select(g => new CalendarProblem(g.Key.Account, g.First().Error!, g.Min(l => l.Copy?.SavedAt), g.Key));
            var others = failed
                .Where(l => l.SignIn is null)
                .Select(l => new CalendarProblem(l.Feed.Name, l.Error!, l.Copy?.SavedAt));
            return [.. signIns, .. others];
        }
    }

    public async Task LoadAsync(CancellationToken cancellationToken)
    {
        var current = MicrosoftFeeds();
        if (!HasData)
        {
            Replace(current.Select(f => new LoadedFeed(f, cache.Load(f), null, null)).Where(l => l.Copy is not null).ToList());
        }

        var today = DateOnly.FromDateTime(time.GetLocalNow().DateTime);
        var from = LocalTime.StartOfDay(today.AddDays(-DaysBack));
        var to = LocalTime.StartOfDay(today.AddDays(DaysAhead + 1));
        Replace(await Task.WhenAll(current.Select(f => SyncFeedAsync(f, from, to, cancellationToken))), loadedAt: time.GetUtcNow());
        cache.RemoveAllExcept(current);
    }

    public IReadOnlyList<CalendarEntry> GetEntries(DateOnly from, DateOnly toExclusive)
    {
        var rangeStart = LocalTime.StartOfDay(from);
        var rangeEnd = LocalTime.StartOfDay(toExclusive);

        return Current()
            .SelectMany(c => (c.Loaded.Copy?.Entries ?? []).Select(e => e with { Source = c.Feed.Name, Color = c.Feed.Color }))
            .Where(e => e.Start < rangeEnd && e.End > rangeStart)
            .OrderBy(e => e.Start)
            .ThenBy(e => e.Title, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    private async Task<LoadedFeed> SyncFeedAsync(CalendarFeed feed, DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken)
    {
        var previous = _loaded.TryGetValue(feed.Id, out var loaded) && loaded.Feed.Source == feed.Source ? loaded.Copy : cache.Load(feed);
        try
        {
            var deltaLink = previous is not null && previous.From == from && previous.To == to ? previous.DeltaLink : null;
            var changes = await graph.SyncAsync(feed, from, to, deltaLink, cancellationToken);
            var copy = new GraphCalendarCopy(from, to, changes.DeltaLink, changes.ApplyTo(previous?.Entries ?? []), time.GetUtcNow());
            cache.Save(feed, copy);
            return new LoadedFeed(feed, copy, null, null);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            var signIn = ex is SignInRequiredException && feed.Source is MicrosoftSource ms ? new AccountSignIn(ms.AccountId, ms.Account) : null;
            var error = signIn is null ? GraphErrors.Describe(ex) : "Your calendars can't update until you sign in";
            return new LoadedFeed(feed, previous, error, signIn);
        }
    }

    private void Replace(IReadOnlyList<LoadedFeed> loaded, DateTimeOffset? loadedAt = null)
    {
        lock (_lock)
        {
            _loaded = loaded.ToDictionary(l => l.Feed.Id);
            _loadedAt = loadedAt ?? _loadedAt;
        }

        Updated?.Invoke(this, EventArgs.Empty);
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

    private sealed record LoadedFeed(CalendarFeed Feed, GraphCalendarCopy? Copy, string? Error, AccountSignIn? SignIn);
}
