using CmdPalCalendar.Events;
using CmdPalCalendar.Feeds;
using CmdPalCalendar.Graph;

namespace CmdPalCalendar.Tests;

public sealed class GraphCalendarSourceTests
{
    private static readonly DateOnly Monday = new(2026, 10, 5);

    private readonly ManualTimeProvider _time = new(new DateTimeOffset(2026, 10, 5, 8, 0, 0, TimeSpan.Zero));
    private readonly FakeEvents _graph = new();
    private List<CalendarFeed> _feeds = [Feed("Work")];

    [Fact]
    public async Task Fetches_two_weeks_back_to_three_months_ahead()
    {
        await Source().LoadAsync(CancellationToken.None);

        var (from, to) = Assert.Single(_graph.Requests);
        Assert.Equal(Monday.AddDays(-14), DateOnly.FromDateTime(from.LocalDateTime));
        Assert.Equal(Monday.AddDays(91), DateOnly.FromDateTime(to.LocalDateTime));
    }

    [Fact]
    public async Task Returns_entries_in_the_range_with_the_current_name_and_colour()
    {
        _graph.Entries = [Entry("Standup", Monday, 9), Entry("Tomorrow", Monday.AddDays(1), 9)];
        var source = Source();
        await source.LoadAsync(CancellationToken.None);
        _feeds = [_feeds[0] with { Name = "Renamed", Color = CalendarColor.Red }];

        var entry = Assert.Single(source.GetEntries(Monday, Monday.AddDays(1)));

        Assert.Equal("Standup", entry.Title);
        Assert.Equal("Renamed", entry.Source);
        Assert.Equal(CalendarColor.Red, entry.Color);
    }

    [Fact]
    public async Task Ignores_ics_calendars()
    {
        _feeds = [CalendarFeed.Create("Feed", new IcsSource("https://example.com/feed.ics"), CalendarColor.Blue)];

        await Source().LoadAsync(CancellationToken.None);

        Assert.Empty(_graph.Requests);
    }

    [Fact]
    public async Task Keeps_the_last_good_entries_when_a_refresh_fails()
    {
        _graph.Entries = [Entry("Standup", Monday, 9)];
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

    private static CalendarFeed Feed(string name) =>
        CalendarFeed.Create(name, new MicrosoftSource("account-1", "ben@example.com", $"calendar-{name}"), CalendarColor.Blue);

    private static CalendarEntry Entry(string title, DateOnly day, int hour)
    {
        var start = LocalTime.FromWallClock(day.ToDateTime(new TimeOnly(hour, 0)));
        return new CalendarEntry(title, title, start, start.AddMinutes(30), false, null, null, null, [], null, "Old name");
    }

    private GraphCalendarSource Source() => new(() => _feeds, _graph, _time);

    private sealed class FakeEvents : IGraphEvents
    {
        public IReadOnlyList<CalendarEntry> Entries { get; set; } = [];

        public Exception? Failure { get; set; }

        public List<(DateTimeOffset From, DateTimeOffset To)> Requests { get; } = [];

        public Task<IReadOnlyList<CalendarEntry>> GetEntriesAsync(CalendarFeed feed, DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken)
        {
            Requests.Add((from, to));
            return Failure is null ? Task.FromResult(Entries) : Task.FromException<IReadOnlyList<CalendarEntry>>(Failure);
        }

        public async Task<GraphChanges> SyncAsync(CalendarFeed feed, DateTimeOffset from, DateTimeOffset to, string? deltaLink, CancellationToken cancellationToken) =>
            new(await GetEntriesAsync(feed, from, to, cancellationToken), new HashSet<string>(), null, IsFull: true);
    }
}
