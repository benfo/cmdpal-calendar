using CmdPalCalendar.Feeds;
using CmdPalCalendar.Ics;

namespace CmdPalCalendar.Tests;

public sealed class IcsCalendarSourceTests : IDisposable
{
    private static readonly DateOnly Monday = new(2026, 10, 5);

    private readonly List<string> _files = [];
    private readonly ManualTimeProvider _time = new(new DateTimeOffset(2026, 10, 5, 8, 0, 0, TimeSpan.Zero));
    private readonly string _cacheDirectory = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}");
    private readonly IcsFeedCache _cache;

    public IcsCalendarSourceTests() => _cache = new IcsFeedCache(_cacheDirectory, new PlainProtector(), _time);

    [Fact]
    public async Task Expands_recurrences_across_the_range()
    {
        var source = await LoadAsync(Event("standup", "Standup", "20260101T090000", "20260101T091500", "RRULE:FREQ=DAILY"));

        var entries = source.GetEntries(Monday, Monday.AddDays(3));

        Assert.Equal(3, entries.Count);
        Assert.All(entries, e => Assert.Equal(new TimeOnly(9, 0), TimeOnly.FromDateTime(e.Start.DateTime)));
        Assert.Equal([Monday, Monday.AddDays(1), Monday.AddDays(2)], entries.Select(e => DateOnly.FromDateTime(e.Start.DateTime)));
    }

    [Fact]
    public async Task Moved_occurrence_replaces_the_series_instance()
    {
        var source = await LoadAsync(
            Event("standup", "Standup", "20260101T090000", "20260101T091500", "RRULE:FREQ=DAILY"),
            Event("standup", "Standup (moved)", "20261005T100000", "20261005T101500", "RECURRENCE-ID:20261005T090000"));

        var entry = Assert.Single(source.GetEntries(Monday, Monday.AddDays(1)));

        Assert.Equal("Standup (moved)", entry.Title);
        Assert.Equal(new TimeOnly(10, 0), TimeOnly.FromDateTime(entry.Start.DateTime));
    }

    [Fact]
    public async Task All_day_event_only_shows_on_its_day()
    {
        var source = await LoadAsync(Event("holiday", "Holiday", "20261005", "20261006", valueType: "DATE"));

        Assert.True(Assert.Single(source.GetEntries(Monday, Monday.AddDays(1))).IsAllDay);
        Assert.Empty(source.GetEntries(Monday.AddDays(1), Monday.AddDays(2)));
    }

    [Fact]
    public async Task Event_that_started_earlier_overlaps_the_day()
    {
        var source = await LoadAsync(Event("trip", "Conference", "20261003T090000", "20261007T170000"));

        Assert.Single(source.GetEntries(Monday, Monday.AddDays(1)));
    }

    [Fact]
    public async Task Cancelled_events_are_hidden()
    {
        var source = await LoadAsync(Event("gone", "Gone", "20261005T090000", "20261005T100000", "STATUS:CANCELLED"));

        Assert.Empty(source.GetEntries(Monday, Monday.AddDays(1)));
    }

    [Fact]
    public async Task Broken_feed_is_reported_without_hiding_others()
    {
        var good = WriteFeed(Event("standup", "Standup", "20261005T090000", "20261005T091500"));
        var source = Source(good, Path.Combine(Path.GetTempPath(), "missing.ics"));

        await source.LoadAsync(CancellationToken.None);

        Assert.Single(source.GetEntries(Monday, Monday.AddDays(1)));
        Assert.Contains("missing.ics", Assert.Single(source.Errors));
    }

    [Fact]
    public async Task Notes_come_from_the_html_description_when_there_is_one()
    {
        var source = await LoadAsync(Event(
            "review",
            "Review",
            "20261005T090000",
            "20261005T100000",
            "DESCRIPTION:Agenda plain\nX-ALT-DESC;FMTTYPE=text/html:<html><body><b>Agenda</b> rich</body></html>"));

        var entry = Assert.Single(source.GetEntries(Monday, Monday.AddDays(1)));

        Assert.Equal("Agenda plain", entry.Description);
        Assert.Equal("**Agenda** rich", entry.Notes);
    }

    [Fact]
    public async Task Sample_feed_has_weekday_events_only()
    {
        var source = Source(Path.Combine(AppContext.BaseDirectory, "sample.ics"));
        await source.LoadAsync(CancellationToken.None);
        var saturday = Monday.AddDays(-2);

        Assert.Empty(source.GetEntries(saturday, Monday));
        Assert.Equal(5, source.GetEntries(Monday, Monday.AddDays(1)).Count);
    }

    [Fact]
    public async Task Is_stale_until_loaded_and_again_after_five_minutes()
    {
        var source = Source(WriteFeed());
        Assert.True(source.IsStale);

        await source.LoadAsync(CancellationToken.None);
        Assert.False(source.IsStale);

        _time.Advance(TimeSpan.FromMinutes(6));
        Assert.True(source.IsStale);
    }

    [Fact]
    public async Task Saves_each_downloaded_calendar_to_the_cache()
    {
        var feed = Feed(WriteFeed(Event("standup", "Standup", "20261005T090000", "20261005T091500")));
        var source = new IcsCalendarSource(() => [feed], new IcsFeedReader(), _cache, _time);

        await source.LoadAsync(CancellationToken.None);

        Assert.Contains("SUMMARY:Standup", _cache.Load(feed)!.Ics, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Is_stale_when_the_feeds_change()
    {
        List<CalendarFeed> feeds = [Feed(WriteFeed())];
        var source = new IcsCalendarSource(() => feeds, new IcsFeedReader(), _cache, _time);
        await source.LoadAsync(CancellationToken.None);

        feeds.Add(Feed(WriteFeed()));

        Assert.True(source.IsStale);
    }

    [Fact]
    public async Task Renaming_a_calendar_shows_without_reloading()
    {
        List<CalendarFeed> feeds = [Feed(WriteFeed(Event("standup", "Standup", "20261005T090000", "20261005T091500")))];
        var source = new IcsCalendarSource(() => feeds, new IcsFeedReader(), _cache, _time);
        await source.LoadAsync(CancellationToken.None);

        feeds[0] = feeds[0] with { Name = "Work" };

        Assert.False(source.IsStale);
        Assert.Equal("Work", Assert.Single(source.GetEntries(Monday, Monday.AddDays(1))).Source);
    }

    [Fact]
    public async Task Turned_off_calendars_are_hidden_without_reloading()
    {
        List<CalendarFeed> feeds = [Feed(WriteFeed(Event("standup", "Standup", "20261005T090000", "20261005T091500")))];
        var source = new IcsCalendarSource(() => feeds.Where(f => f.Enabled).ToList(), new IcsFeedReader(), _cache, _time);
        await source.LoadAsync(CancellationToken.None);

        feeds[0] = feeds[0] with { Enabled = false };

        Assert.Empty(source.GetEntries(Monday, Monday.AddDays(1)));
    }

    public void Dispose()
    {
        foreach (var file in _files)
        {
            File.Delete(file);
        }

        if (Directory.Exists(_cacheDirectory))
        {
            Directory.Delete(_cacheDirectory, recursive: true);
        }
    }

    private async Task<IcsCalendarSource> LoadAsync(params string[] events)
    {
        var source = Source(WriteFeed(events));
        await source.LoadAsync(CancellationToken.None);
        return source;
    }

    private IcsCalendarSource Source(params string[] paths)
    {
        var feeds = paths.Select(Feed).ToList();
        return new IcsCalendarSource(() => feeds, new IcsFeedReader(), _cache, _time);
    }

    private static CalendarFeed Feed(string path) => CalendarFeed.Create(CalendarFeed.Describe(path), path, CalendarColor.Blue);

    private string WriteFeed(params string[] events)
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.ics");
        File.WriteAllText(path, $"BEGIN:VCALENDAR\nVERSION:2.0\nPRODID:-//tests//EN\n{string.Concat(events)}END:VCALENDAR\n");
        _files.Add(path);
        return path;
    }

    private static string Event(string uid, string summary, string start, string end, string? extra = null, string? valueType = null)
    {
        var value = valueType is null ? string.Empty : $";VALUE={valueType}";
        var extraLine = extra is null ? string.Empty : $"{extra}\n";
        return $"BEGIN:VEVENT\nUID:{uid}\nSUMMARY:{summary}\nDTSTART{value}:{start}\nDTEND{value}:{end}\n{extraLine}END:VEVENT\n";
    }
}
