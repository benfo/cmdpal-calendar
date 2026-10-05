using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CmdPalCalendar.Events;
using CmdPalCalendar.Feeds;

namespace CmdPalCalendar.Graph;

internal sealed class GraphCalendarSource : ICalendarSource
{
    private const int DaysBack = 14;
    private const int DaysAhead = 90;
    private const string SignInMessage = "Your calendars can't update until you sign in";
    private static readonly TimeSpan MaxAge = TimeSpan.FromMinutes(5);

    private readonly Func<IReadOnlyList<CalendarFeed>> _feeds;
    private readonly IGraphEvents _graph;
    private readonly GraphEventCache _cache;
    private readonly TimeProvider _time;
    private readonly GraphMonths _months;
    private readonly Lock _lock = new();
    private IReadOnlyDictionary<string, LoadedFeed> _loaded = new Dictionary<string, LoadedFeed>();
    private DateTimeOffset? _loadedAt;

    public GraphCalendarSource(Func<IReadOnlyList<CalendarFeed>> feeds, IGraphEvents graph, GraphEventCache cache, TimeProvider time)
    {
        _feeds = feeds;
        _graph = graph;
        _cache = cache;
        _time = time;
        _months = new GraphMonths(graph, time);
        _months.Updated += (_, _) => Updated?.Invoke(this, EventArgs.Empty);
    }

    public event EventHandler? Updated;

    public bool IsFetching => _months.IsFetching;

    public bool IsStale =>
        _loadedAt is not { } loadedAt ||
        _time.GetUtcNow() - loadedAt > MaxAge ||
        !Keys(_loaded.Values.Select(l => l.Feed)).SetEquals(Keys(MicrosoftFeeds()));

    public bool HasData => _loaded.Values.Any(l => l.Copy is not null);

    public IReadOnlyList<CalendarProblem> Problems
    {
        get
        {
            var failed = Current()
                .Select(c => c.Loaded)
                .Where(l => l.Error is not null)
                .Select(l => new Failure(l.Feed.Name, l.Error!, l.SignIn, l.Copy?.SavedAt))
                .Concat(_months.Failures.Select(f =>
                    new Failure($"{f.Feed.Name} for {MonthName(f.Month)}", GraphErrors.Describe(f.Error), SignInFor(f.Feed, f.Error), f.ShowingCopyFrom)))
                .ToList();

            var signIns = failed
                .Where(f => f.SignIn is not null)
                .GroupBy(f => f.SignIn!)
                .Select(g => new CalendarProblem(g.Key.Account, SignInMessage, g.Min(f => f.SavedAt), g.Key));
            var others = failed
                .Where(f => f.SignIn is null)
                .Select(f => new CalendarProblem(f.Calendar, f.Message, f.SavedAt));
            return [.. signIns, .. others];
        }
    }

    public async Task LoadAsync(CancellationToken cancellationToken)
    {
        var current = MicrosoftFeeds();
        _months.Retain(current);
        _months.Expire();
        if (!HasData)
        {
            Replace(current.Select(f => new LoadedFeed(f, _cache.Load(f), null, null)).Where(l => l.Copy is not null).ToList());
        }

        var (from, to) = Window();
        Replace(await Task.WhenAll(current.Select(f => SyncFeedAsync(f, from, to, cancellationToken))), loadedAt: _time.GetUtcNow());
        _cache.RemoveAllExcept(current);
    }

    public IReadOnlyList<CalendarEntry> GetEntries(DateOnly from, DateOnly toExclusive)
    {
        var rangeStart = LocalTime.StartOfDay(from);
        var rangeEnd = LocalTime.StartOfDay(toExclusive);
        var loaded = _loaded;

        return MicrosoftFeeds()
            .SelectMany(f => EntriesOf(f, loaded.TryGetValue(f.Id, out var l) && l.Feed.Source == f.Source ? l.Copy : null, rangeStart, rangeEnd))
            .Where(e => e.Start < rangeEnd && e.End > rangeStart)
            .OrderBy(e => e.Start)
            .ThenBy(e => e.Title, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    private IEnumerable<CalendarEntry> EntriesOf(CalendarFeed feed, GraphCalendarCopy? copy, DateTimeOffset from, DateTimeOffset to)
    {
        var (windowFrom, windowTo) = copy is null ? Window() : (copy.From, copy.To);
        (DateTimeOffset From, DateTimeOffset To)[] outside = [(from, Min(to, windowFrom)), (Max(from, windowTo), to)];

        return (copy?.Entries ?? [])
            .Where(e => e.Start < windowTo && e.End > windowFrom)
            .Concat(outside.Where(r => r.From < r.To).SelectMany(r => _months.GetEntries(feed, r.From, r.To)))
            .DistinctBy(e => e.Uid)
            .Select(e => e with { Source = feed.Name, Color = feed.Color });
    }

    private (DateTimeOffset From, DateTimeOffset To) Window()
    {
        var today = DateOnly.FromDateTime(_time.GetLocalNow().DateTime);
        return (LocalTime.StartOfDay(today.AddDays(-DaysBack)), LocalTime.StartOfDay(today.AddDays(DaysAhead + 1)));
    }

    private async Task<LoadedFeed> SyncFeedAsync(CalendarFeed feed, DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken)
    {
        var previous = _loaded.TryGetValue(feed.Id, out var loaded) && loaded.Feed.Source == feed.Source ? loaded.Copy : _cache.Load(feed);
        try
        {
            var deltaLink = previous is not null && previous.From == from && previous.To == to ? previous.DeltaLink : null;
            var changes = await _graph.SyncAsync(feed, from, to, deltaLink, cancellationToken);
            var copy = new GraphCalendarCopy(from, to, changes.DeltaLink, changes.ApplyTo(previous?.Entries ?? []), _time.GetUtcNow());
            _cache.Save(feed, copy);
            return new LoadedFeed(feed, copy, null, null);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            var signIn = SignInFor(feed, ex);
            return new LoadedFeed(feed, previous, signIn is null ? GraphErrors.Describe(ex) : SignInMessage, signIn);
        }
    }

    private static AccountSignIn? SignInFor(CalendarFeed feed, Exception error) =>
        error is SignInRequiredException && feed.Source is MicrosoftSource ms ? new AccountSignIn(ms.AccountId, ms.Account) : null;

    private static string MonthName(DateOnly month) => month.ToString("MMMM yyyy", CultureInfo.CurrentCulture);

    private static DateTimeOffset Min(DateTimeOffset a, DateTimeOffset b) => a < b ? a : b;

    private static DateTimeOffset Max(DateTimeOffset a, DateTimeOffset b) => a > b ? a : b;

    private void Replace(IReadOnlyList<LoadedFeed> loaded, DateTimeOffset? loadedAt = null)
    {
        lock (_lock)
        {
            _loaded = loaded.ToDictionary(l => l.Feed.Id);
            _loadedAt = loadedAt ?? _loadedAt;
        }

        Updated?.Invoke(this, EventArgs.Empty);
    }

    private List<CalendarFeed> MicrosoftFeeds() => _feeds().Where(f => f.Source is MicrosoftSource).ToList();

    private IEnumerable<(CalendarFeed Feed, LoadedFeed Loaded)> Current()
    {
        var loaded = _loaded;
        return MicrosoftFeeds()
            .Where(f => loaded.TryGetValue(f.Id, out var l) && l.Feed.Source == f.Source)
            .Select(f => (f, loaded[f.Id]));
    }

    private static HashSet<(string Id, FeedSource Source)> Keys(IEnumerable<CalendarFeed> feeds) =>
        feeds.Select(f => (f.Id, f.Source)).ToHashSet();

    private sealed record Failure(string Calendar, string Message, AccountSignIn? SignIn, DateTimeOffset? SavedAt);

    private sealed record LoadedFeed(CalendarFeed Feed, GraphCalendarCopy? Copy, string? Error, AccountSignIn? SignIn);
}
