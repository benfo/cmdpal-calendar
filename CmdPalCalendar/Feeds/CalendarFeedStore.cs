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

    private StoredFeed ToStored(CalendarFeed feed) => feed.Source switch
    {
        IcsSource ics => new(feed.Id, feed.Name, Location: null, Protect(ics.Location), feed.Enabled, feed.Color),
        MicrosoftSource ms => new(feed.Id, feed.Name, Location: null, ProtectedLocation: null, feed.Enabled, feed.Color, new StoredMicrosoft(ms.AccountId, ms.Account, ms.CalendarId)),
        _ => throw new NotSupportedException($"Unknown calendar source {feed.Source.GetType().Name}"),
    };

    private CalendarFeed ToFeed(StoredFeed stored) =>
        new(stored.Id, stored.Name, ToSource(stored), stored.Enabled, stored.Color);

    private FeedSource ToSource(StoredFeed stored) => stored.Microsoft is { } ms
        ? new MicrosoftSource(ms.AccountId, ms.Account, ms.CalendarId)
        : new IcsSource(stored.Location ?? Unprotect(stored.ProtectedLocation));

    private string Protect(string location) => Convert.ToBase64String(_protector.Protect(Encoding.UTF8.GetBytes(location)));

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

    private sealed record StoredFeed(string Id, string Name, string? Location, string? ProtectedLocation, bool Enabled, CalendarColor Color, StoredMicrosoft? Microsoft = null);

    private sealed record StoredMicrosoft(string AccountId, string Account, string CalendarId);

    [JsonSourceGenerationOptions(
        WriteIndented = true,
        PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
        UseStringEnumConverter = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonSerializable(typeof(List<StoredFeed>))]
    private sealed partial class StoreJsonContext : JsonSerializerContext;
}
