using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CmdPalCalendar.Feeds;

namespace CmdPalCalendar.Ics;

internal sealed record CachedFeed(string Ics, DateTimeOffset SavedAt);

internal sealed class IcsFeedCache(string directory, ISecretProtector protector, TimeProvider time)
{
    private readonly EncryptedFiles _files = new(directory, ".ics.bin", protector);

    public void Save(CalendarFeed feed, string ics) =>
        _files.Write(feed.Id, Encoding.UTF8.GetBytes($"{feed.IcsLocation()}\n{ics}"), time.GetUtcNow());

    public CachedFeed? Load(CalendarFeed feed)
    {
        if (_files.Read(feed.Id) is not { } file)
        {
            return null;
        }

        var content = Encoding.UTF8.GetString(file.Content);
        var separator = content.IndexOf('\n', StringComparison.Ordinal);
        return separator >= 0 && content[..separator] == feed.IcsLocation()
            ? new CachedFeed(content[(separator + 1)..], file.WrittenAt)
            : null;
    }

    public void RemoveAllExcept(IEnumerable<CalendarFeed> feeds) => _files.RemoveAllExcept(feeds.Select(f => f.Id));
}
