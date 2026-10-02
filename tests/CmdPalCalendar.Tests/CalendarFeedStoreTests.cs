using CmdPalCalendar.Feeds;

namespace CmdPalCalendar.Tests;

public sealed class CalendarFeedStoreTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}", "calendars.json");

    [Fact]
    public void Starts_empty_without_a_file()
    {
        Assert.Empty(new CalendarFeedStore(_path, new PlainProtector()).Feeds);
    }

    [Fact]
    public void Saves_and_reloads_calendars()
    {
        var store = new CalendarFeedStore(_path, new PlainProtector());
        var work = CalendarFeed.Create("Work", "https://example.com/work.ics", CalendarColor.Purple);

        store.Add(work);
        store.Add(CalendarFeed.Create("Family", @"C:\cal\family.ics", CalendarColor.Green));

        var reloaded = new CalendarFeedStore(_path, new PlainProtector()).Feeds;
        Assert.Equal(["Work", "Family"], reloaded.Select(f => f.Name));
        Assert.Equal(work, reloaded[0]);
    }

    [Fact]
    public void Updates_and_removes_by_id()
    {
        var store = new CalendarFeedStore(_path, new PlainProtector());
        var work = CalendarFeed.Create("Work", "https://example.com/work.ics", CalendarColor.Blue);
        var family = CalendarFeed.Create("Family", "https://example.com/family.ics", CalendarColor.Green);
        store.Add(work);
        store.Add(family);

        store.Update(work with { Name = "Office", Enabled = false });
        store.Remove(family.Id);

        var feed = Assert.Single(new CalendarFeedStore(_path, new PlainProtector()).Feeds);
        Assert.Equal("Office", feed.Name);
        Assert.False(feed.Enabled);
        Assert.Empty(store.EnabledFeeds);
    }

    [Fact]
    public void Raises_changed_after_saving()
    {
        var store = new CalendarFeedStore(_path, new PlainProtector());
        var raised = 0;
        store.Changed += (_, _) => raised++;

        store.Add(CalendarFeed.Create("Work", "https://example.com/work.ics", CalendarColor.Blue));

        Assert.Equal(1, raised);
    }

    [Fact]
    public void Suggests_colours_in_turn()
    {
        var store = new CalendarFeedStore(_path, new PlainProtector());
        Assert.Equal(CalendarColor.Blue, store.NextColor);

        store.Add(CalendarFeed.Create("Work", "https://example.com/work.ics", store.NextColor));

        Assert.Equal(CalendarColor.Purple, store.NextColor);
    }

    [Fact]
    public void Stores_addresses_encrypted_and_the_rest_readable()
    {
        new CalendarFeedStore(_path, new PlainProtector())
            .Add(CalendarFeed.Create("Work", "https://example.com/secret-token/work.ics", CalendarColor.Purple));

        var json = File.ReadAllText(_path);
        Assert.DoesNotContain("secret-token", json, StringComparison.Ordinal);
        Assert.Contains("\"name\": \"Work\"", json, StringComparison.Ordinal);
        Assert.Contains("\"color\": \"Purple\"", json, StringComparison.Ordinal);
    }

    [Fact]
    public void Encrypts_plain_addresses_from_an_older_file()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        File.WriteAllText(_path, """[{ "id": "1", "name": "Work", "location": "https://example.com/secret-token/work.ics", "enabled": true, "color": "Blue" }]""");

        var feed = Assert.Single(new CalendarFeedStore(_path, new PlainProtector()).Feeds);

        Assert.Equal("https://example.com/secret-token/work.ics", feed.Location);
        Assert.DoesNotContain("secret-token", File.ReadAllText(_path), StringComparison.Ordinal);
    }

    [Fact]
    public void An_address_that_cannot_be_decrypted_comes_back_empty()
    {
        new CalendarFeedStore(_path, new PlainProtector()).Add(CalendarFeed.Create("Work", "https://example.com/work.ics", CalendarColor.Blue));

        var feed = Assert.Single(new CalendarFeedStore(_path, new FailingProtector()).Feeds);

        Assert.Equal("Work", feed.Name);
        Assert.Equal(string.Empty, feed.Location);
    }

    [Fact]
    public void Ignores_a_corrupt_file()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        File.WriteAllText(_path, "{ not json");

        Assert.Empty(new CalendarFeedStore(_path, new PlainProtector()).Feeds);
    }

    public void Dispose()
    {
        var directory = Path.GetDirectoryName(_path)!;
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
