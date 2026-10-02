using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;

namespace CmdPalCalendar.Feeds;

internal sealed partial class CalendarFeedStore
{
    private readonly string _path;
    private readonly ISecretProtector _protector;
    private readonly Lock _lock = new();
    private IReadOnlyList<CalendarFeed> _feeds;

    public CalendarFeedStore(string path, ISecretProtector protector)
    {
        _path = path;
        _protector = protector;

        var stored = Read(path);
        _feeds = stored.Select(ToFeed).ToList();
        if (stored.Any(s => s.Location is not null))
        {
            Write();
        }
    }

    public event EventHandler? Changed;

    public IReadOnlyList<CalendarFeed> Feeds => _feeds;

    public IReadOnlyList<CalendarFeed> EnabledFeeds => _feeds.Where(f => f.Enabled).ToList();

    public CalendarColor NextColor =>
        Enum.GetValues<CalendarColor>().ElementAt(_feeds.Count % Enum.GetValues<CalendarColor>().Length);

    public void Add(CalendarFeed feed) => Change(feeds => [.. feeds, feed]);

    public void Update(CalendarFeed feed) => Change(feeds => feeds.Select(f => f.Id == feed.Id ? feed : f).ToList());

    public void Remove(string id) => Change(feeds => feeds.Where(f => f.Id != id).ToList());

    private void Change(Func<IReadOnlyList<CalendarFeed>, IReadOnlyList<CalendarFeed>> change)
    {
        lock (_lock)
        {
            _feeds = change(_feeds);
            Write();
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void Write()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        File.WriteAllText(_path, JsonSerializer.Serialize(_feeds.Select(ToStored).ToList(), StoreJsonContext.Default.ListStoredFeed));
    }

    private StoredFeed ToStored(CalendarFeed feed) =>
        new(feed.Id, feed.Name, Location: null, Convert.ToBase64String(_protector.Protect(Encoding.UTF8.GetBytes(feed.Location))), feed.Enabled, feed.Color);

    private CalendarFeed ToFeed(StoredFeed stored) =>
        new(stored.Id, stored.Name, stored.Location ?? Unprotect(stored.ProtectedLocation), stored.Enabled, stored.Color);

    private string Unprotect(string? protectedLocation)
    {
        try
        {
            return protectedLocation is null ? string.Empty : Encoding.UTF8.GetString(_protector.Unprotect(Convert.FromBase64String(protectedLocation)));
        }
        catch (Exception ex) when (ex is CryptographicException or FormatException)
        {
            return string.Empty;
        }
    }

    private static List<StoredFeed> Read(string path)
    {
        if (!File.Exists(path))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize(File.ReadAllText(path), StoreJsonContext.Default.ListStoredFeed) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private sealed record StoredFeed(string Id, string Name, string? Location, string? ProtectedLocation, bool Enabled, CalendarColor Color);

    [JsonSourceGenerationOptions(
        WriteIndented = true,
        PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
        UseStringEnumConverter = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonSerializable(typeof(List<StoredFeed>))]
    private sealed partial class StoreJsonContext : JsonSerializerContext;
}
