using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using CmdPalCalendar.Events;
using CmdPalCalendar.Feeds;

namespace CmdPalCalendar.Graph;

internal sealed record GraphCalendarCopy(DateTimeOffset From, DateTimeOffset To, string? DeltaLink, IReadOnlyList<CalendarEntry> Entries, DateTimeOffset SavedAt);

internal sealed partial class GraphEventCache(string directory, ISecretProtector protector)
{
    private readonly EncryptedFiles _files = new(directory, ".graph.bin", protector);

    public void Save(CalendarFeed feed, GraphCalendarCopy copy)
    {
        var source = (MicrosoftSource)feed.Source;
        var stored = new StoredCopy(source.AccountId, source.CalendarId, copy.From, copy.To, copy.DeltaLink, copy.Entries.ToList());
        _files.Write(feed.Id, JsonSerializer.SerializeToUtf8Bytes(stored, CacheJsonContext.Default.StoredCopy), copy.SavedAt);
    }

    public GraphCalendarCopy? Load(CalendarFeed feed)
    {
        if (_files.Read(feed.Id) is not { } file || feed.Source is not MicrosoftSource source)
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize(file.Content, CacheJsonContext.Default.StoredCopy) is { } stored &&
                stored.AccountId == source.AccountId && stored.CalendarId == source.CalendarId
                ? new GraphCalendarCopy(stored.From, stored.To, stored.DeltaLink, stored.Entries, file.WrittenAt)
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public void RemoveAllExcept(IEnumerable<CalendarFeed> feeds) => _files.RemoveAllExcept(feeds.Select(f => f.Id));

    private sealed record StoredCopy(string AccountId, string CalendarId, DateTimeOffset From, DateTimeOffset To, string? DeltaLink, List<CalendarEntry> Entries);

    [JsonSourceGenerationOptions(UseStringEnumConverter = true, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonSerializable(typeof(StoredCopy))]
    private sealed partial class CacheJsonContext : JsonSerializerContext;
}
