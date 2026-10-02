using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;

namespace CmdPalCalendar.Feeds;

internal sealed partial class CalendarFeedStore
{
    private readonly string _path;
    private readonly Lock _lock = new();
    private IReadOnlyList<CalendarFeed> _feeds;

    public CalendarFeedStore(string path)
    {
        _path = path;
        _feeds = Read(path);
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
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            File.WriteAllText(_path, JsonSerializer.Serialize(_feeds.ToList(), FeedsJsonContext.Default.ListCalendarFeed));
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    private static List<CalendarFeed> Read(string path)
    {
        if (!File.Exists(path))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize(File.ReadAllText(path), FeedsJsonContext.Default.ListCalendarFeed) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    [JsonSourceGenerationOptions(WriteIndented = true, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, UseStringEnumConverter = true)]
    [JsonSerializable(typeof(List<CalendarFeed>))]
    private sealed partial class FeedsJsonContext : JsonSerializerContext;
}
