using System.Text;
using CmdPalCalendar.Feeds;
using CmdPalCalendar.Ics;

namespace CmdPalCalendar.Tests;

public sealed class IcsFeedCacheTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}");
    private readonly ManualTimeProvider _time = new(new DateTimeOffset(2026, 10, 5, 8, 0, 0, TimeSpan.Zero));
    private readonly CalendarFeed _work = CalendarFeed.Create("Work", "https://example.com/work.ics", CalendarColor.Blue);

    [Fact]
    public void Returns_what_was_saved_and_when()
    {
        var cache = Cache();

        cache.Save(_work, "BEGIN:VCALENDAR\nEND:VCALENDAR");

        Assert.Equal(new CachedFeed("BEGIN:VCALENDAR\nEND:VCALENDAR", _time.GetUtcNow()), cache.Load(_work));
    }

    [Fact]
    public void Stores_the_feed_protected()
    {
        Cache().Save(_work, "BEGIN:VCALENDAR\nSUMMARY:Secret meeting\nEND:VCALENDAR");

        var file = Assert.Single(Directory.GetFiles(_directory));
        Assert.DoesNotContain("Secret meeting", Encoding.UTF8.GetString(File.ReadAllBytes(file)), StringComparison.Ordinal);
    }

    [Fact]
    public void Ignores_a_copy_saved_for_another_address()
    {
        var cache = Cache();
        cache.Save(_work, "old");

        Assert.Null(cache.Load(_work with { Location = "https://example.com/new.ics" }));
    }

    [Fact]
    public void Ignores_a_copy_that_cannot_be_decrypted()
    {
        Cache().Save(_work, "data");

        Assert.Null(new IcsFeedCache(_directory, new FailingProtector(), _time).Load(_work));
    }

    [Fact]
    public void Returns_nothing_when_never_saved()
    {
        Assert.Null(Cache().Load(_work));
    }

    [Fact]
    public void Removes_copies_of_calendars_that_are_gone()
    {
        var cache = Cache();
        var family = CalendarFeed.Create("Family", "https://example.com/family.ics", CalendarColor.Green);
        cache.Save(_work, "work");
        cache.Save(family, "family");

        cache.RemoveAllExcept([_work]);

        Assert.NotNull(cache.Load(_work));
        Assert.Null(cache.Load(family));
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    private IcsFeedCache Cache() => new(_directory, new ReversingProtector(), _time);

    private sealed class ReversingProtector : ISecretProtector
    {
        public byte[] Protect(byte[] data) => [.. data.Reverse()];

        public byte[] Unprotect(byte[] data) => [.. data.Reverse()];
    }
}
