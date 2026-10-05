using CmdPalCalendar.Events;
using CmdPalCalendar.Feeds;
using CmdPalCalendar.Graph;

namespace CmdPalCalendar.Tests;

public sealed class GraphCalendarSourceTests : IDisposable
{
    private static readonly DateOnly Monday = new(2026, 10, 5);

    private readonly ManualTimeProvider _time = new(new DateTimeOffset(2026, 10, 5, 8, 0, 0, TimeSpan.Zero));
    private readonly FakeEvents _graph = new();
    private readonly string _cacheDirectory = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}");
    private readonly GraphEventCache _cache;
    private List<CalendarFeed> _feeds = [Feed("Work")];

    public GraphCalendarSourceTests() => _cache = new GraphEventCache(_cacheDirectory, new PlainProtector());

    [Fact]
    public async Task Syncs_two_weeks_back_to_three_months_ahead()
    {
        await Source().LoadAsync(CancellationToken.None);

        var sync = Assert.Single(_graph.Syncs);
        Assert.Equal(Monday.AddDays(-14), DateOnly.FromDateTime(sync.From.LocalDateTime));
        Assert.Equal(Monday.AddDays(91), DateOnly.FromDateTime(sync.To.LocalDateTime));
        Assert.Null(sync.DeltaLink);
    }

    [Fact]
    public async Task Returns_entries_in_the_range_with_the_current_name_and_colour()
    {
        _graph.Next = Full(Entry("e1", "Standup", Monday, 9), Entry("e2", "Tomorrow", Monday.AddDays(1), 9));
        var source = Source();
        await source.LoadAsync(CancellationToken.None);
        _feeds = [_feeds[0] with { Name = "Renamed", Color = CalendarColor.Red }];

        var entry = Assert.Single(source.GetEntries(Monday, Monday.AddDays(1)));

        Assert.Equal("Standup", entry.Title);
        Assert.Equal("Renamed", entry.Source);
        Assert.Equal(CalendarColor.Red, entry.Color);
    }

    [Fact]
    public async Task Applies_changes_from_the_delta_link_on_later_syncs()
    {
        _graph.Next = Full(Entry("e1", "Standup", Monday, 9), Entry("e2", "Review", Monday, 11));
        var source = Source();
        await source.LoadAsync(CancellationToken.None);

        _graph.Next = new GraphChanges([Entry("e3", "Lunch", Monday, 12)], new HashSet<string> { "e2" }, "delta-2", IsFull: false);
        _time.Advance(TimeSpan.FromMinutes(6));
        await source.LoadAsync(CancellationToken.None);

        Assert.Equal("delta-1", _graph.Syncs[1].DeltaLink);
        Assert.Equal(["Standup", "Lunch"], source.GetEntries(Monday, Monday.AddDays(1)).Select(e => e.Title));
    }

    [Fact]
    public async Task Starts_a_full_sync_when_the_day_changes()
    {
        var source = Source();
        await source.LoadAsync(CancellationToken.None);

        _time.Advance(TimeSpan.FromDays(1));
        await source.LoadAsync(CancellationToken.None);

        Assert.Null(_graph.Syncs[1].DeltaLink);
    }

    [Fact]
    public async Task Shows_the_cached_copy_at_startup_and_continues_from_its_delta_link()
    {
        _graph.Next = Full(Entry("e1", "Standup", Monday, 9));
        await Source().LoadAsync(CancellationToken.None);

        var restarted = Source();
        var updates = new List<IReadOnlyList<CalendarEntry>>();
        restarted.Updated += (_, _) => updates.Add(restarted.GetEntries(Monday, Monday.AddDays(1)));
        _graph.Failure = new HttpRequestException("offline");
        await restarted.LoadAsync(CancellationToken.None);

        Assert.Equal("Standup", Assert.Single(updates[0]).Title);
        Assert.Equal("delta-1", _graph.Syncs[1].DeltaLink);
        Assert.Single(restarted.GetEntries(Monday, Monday.AddDays(1)));
        Assert.NotNull(Assert.Single(restarted.Problems).ShowingCopyFrom);
    }

    [Fact]
    public async Task Ignores_ics_calendars()
    {
        _feeds = [CalendarFeed.Create("Feed", new IcsSource("https://example.com/feed.ics"), CalendarColor.Blue)];

        await Source().LoadAsync(CancellationToken.None);

        Assert.Empty(_graph.Syncs);
    }

    [Fact]
    public async Task Keeps_the_last_good_entries_when_a_refresh_fails()
    {
        _graph.Next = Full(Entry("e1", "Standup", Monday, 9));
        var source = Source();
        await source.LoadAsync(CancellationToken.None);
        var loadedAt = _time.GetUtcNow();

        _time.Advance(TimeSpan.FromMinutes(10));
        _graph.Failure = new HttpRequestException("offline");
        await source.LoadAsync(CancellationToken.None);

        Assert.Single(source.GetEntries(Monday, Monday.AddDays(1)));
        var problem = Assert.Single(source.Problems);
        Assert.Equal("Work", problem.Calendar);
        Assert.Equal(loadedAt, problem.ShowingCopyFrom);
    }

    [Fact]
    public async Task Reports_a_calendar_that_never_loaded()
    {
        _graph.Failure = new HttpRequestException("offline");
        var source = Source();

        await source.LoadAsync(CancellationToken.None);

        var problem = Assert.Single(source.Problems);
        Assert.Equal("Couldn't reach Microsoft. Check your connection.", problem.Message);
        Assert.Null(problem.ShowingCopyFrom);
        Assert.Null(problem.SignIn);
        Assert.False(source.HasData);
    }

    [Fact]
    public async Task Asks_once_per_account_to_sign_in_again()
    {
        _feeds = [Feed("Work"), Feed("Team")];
        _graph.Failure = new SignInRequiredException("account-1", "invalid_grant: expired");
        var source = Source();

        await source.LoadAsync(CancellationToken.None);

        var problem = Assert.Single(source.Problems);
        Assert.Equal(new AccountSignIn("account-1", "ben@example.com"), problem.SignIn);
        Assert.Equal("ben@example.com", problem.Calendar);
        Assert.Equal("Your calendars can't update until you sign in", problem.Message);
    }

    [Fact]
    public async Task Forgets_the_cached_copies_of_removed_calendars()
    {
        var team = Feed("Team");
        _feeds = [_feeds[0], team];
        var source = Source();
        await source.LoadAsync(CancellationToken.None);

        _feeds = [_feeds[0]];
        await source.LoadAsync(CancellationToken.None);

        Assert.Null(_cache.Load(team));
    }

    [Fact]
    public async Task Goes_stale_after_five_minutes_or_when_calendars_change()
    {
        var source = Source();
        Assert.True(source.IsStale);

        await source.LoadAsync(CancellationToken.None);
        Assert.False(source.IsStale);

        _feeds = [.. _feeds, Feed("Team")];
        Assert.True(source.IsStale);

        await source.LoadAsync(CancellationToken.None);
        _time.Advance(TimeSpan.FromMinutes(6));
        Assert.True(source.IsStale);
    }

    public void Dispose()
    {
        if (Directory.Exists(_cacheDirectory))
        {
            Directory.Delete(_cacheDirectory, recursive: true);
        }
    }

    private static CalendarFeed Feed(string name) =>
        CalendarFeed.Create(name, new MicrosoftSource("account-1", "ben@example.com", $"calendar-{name}"), CalendarColor.Blue);

    private static GraphChanges Full(params CalendarEntry[] entries) => new(entries, new HashSet<string>(), "delta-1", IsFull: true);

    private static CalendarEntry Entry(string id, string title, DateOnly day, int hour)
    {
        var start = LocalTime.FromWallClock(day.ToDateTime(new TimeOnly(hour, 0)));
        return new CalendarEntry(id, title, start, start.AddMinutes(30), false, null, null, null, [], null, "Old name");
    }

    private GraphCalendarSource Source() => new(() => _feeds, _graph, _cache, _time);

    private sealed class FakeEvents : IGraphEvents
    {
        public GraphChanges Next { get; set; } = Full();

        public Exception? Failure { get; set; }

        public List<(DateTimeOffset From, DateTimeOffset To, string? DeltaLink)> Syncs { get; } = [];

        public Task<IReadOnlyList<CalendarEntry>> GetEntriesAsync(CalendarFeed feed, DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken) =>
            Task.FromResult(Next.Changed);

        public Task<GraphChanges> SyncAsync(CalendarFeed feed, DateTimeOffset from, DateTimeOffset to, string? deltaLink, CancellationToken cancellationToken)
        {
            Syncs.Add((from, to, deltaLink));
            return Failure is null ? Task.FromResult(Next) : Task.FromException<GraphChanges>(Failure);
        }
    }
}
