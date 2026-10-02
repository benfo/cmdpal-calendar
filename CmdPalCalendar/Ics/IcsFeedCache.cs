using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using CmdPalCalendar.Feeds;

namespace CmdPalCalendar.Ics;

internal sealed record CachedFeed(string Ics, DateTimeOffset SavedAt);

internal sealed class IcsFeedCache(string directory, ISecretProtector protector, TimeProvider time)
{
    private const string Extension = ".ics.bin";

    public void Save(CalendarFeed feed, string ics)
    {
        Directory.CreateDirectory(directory);
        var path = PathFor(feed.Id);
        File.WriteAllBytes(path, protector.Protect(Encoding.UTF8.GetBytes($"{feed.Location}\n{ics}")));
        File.SetLastWriteTimeUtc(path, time.GetUtcNow().UtcDateTime);
    }

    public CachedFeed? Load(CalendarFeed feed)
    {
        var path = PathFor(feed.Id);
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            var content = Encoding.UTF8.GetString(protector.Unprotect(File.ReadAllBytes(path)));
            var separator = content.IndexOf('\n', StringComparison.Ordinal);
            return separator >= 0 && content[..separator] == feed.Location
                ? new CachedFeed(content[(separator + 1)..], new DateTimeOffset(File.GetLastWriteTimeUtc(path), TimeSpan.Zero))
                : null;
        }
        catch (Exception ex) when (ex is CryptographicException or IOException)
        {
            return null;
        }
    }

    public void RemoveAllExcept(IEnumerable<CalendarFeed> feeds)
    {
        if (!Directory.Exists(directory))
        {
            return;
        }

        var keep = feeds.Select(f => PathFor(f.Id)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var file in Directory.EnumerateFiles(directory, $"*{Extension}").Where(f => !keep.Contains(f)))
        {
            File.Delete(file);
        }
    }

    private string PathFor(string feedId) => Path.Combine(directory, feedId + Extension);
}
