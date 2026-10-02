using CmdPalCalendar.Feeds;

namespace CmdPalCalendar.Tests;

public sealed class LegacyFeedImportTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}");

    private string SettingsPath => Path.Combine(_directory, "settings.json");

    private string CalendarsPath => Path.Combine(_directory, "calendars.json");

    [Fact]
    public void Imports_each_feed_line_once_and_removes_the_old_setting()
    {
        WriteSettings("""{ "CmdPalCalendar.IcsFeeds": "https://calendar.google.com/a.ics\rhttps://outlook.office365.com/b.ics\r\rhttps://calendar.google.com/a.ics", "Other": 1 }""");
        var store = new CalendarFeedStore(CalendarsPath, new PlainProtector());

        LegacyFeedImport.Run(SettingsPath, store);
        LegacyFeedImport.Run(SettingsPath, store);

        Assert.Equal(["calendar.google.com", "outlook.office365.com"], store.Feeds.Select(f => f.Name));
        Assert.Equal([CalendarColor.Blue, CalendarColor.Purple], store.Feeds.Select(f => f.Color));
        Assert.DoesNotContain("IcsFeeds", File.ReadAllText(SettingsPath), StringComparison.Ordinal);
        Assert.Contains("Other", File.ReadAllText(SettingsPath), StringComparison.Ordinal);
    }

    [Fact]
    public void Leaves_existing_calendars_alone()
    {
        WriteSettings("""{ "CmdPalCalendar.IcsFeeds": "https://calendar.google.com/a.ics" }""");
        var store = new CalendarFeedStore(CalendarsPath, new PlainProtector());
        store.Add(CalendarFeed.Create("Work", "https://example.com/work.ics", CalendarColor.Red));

        LegacyFeedImport.Run(SettingsPath, store);

        Assert.Equal("Work", Assert.Single(store.Feeds).Name);
    }

    [Fact]
    public void Does_nothing_without_a_settings_file()
    {
        var store = new CalendarFeedStore(CalendarsPath, new PlainProtector());

        LegacyFeedImport.Run(SettingsPath, store);

        Assert.Empty(store.Feeds);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    private void WriteSettings(string json)
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(SettingsPath, json);
    }
}
