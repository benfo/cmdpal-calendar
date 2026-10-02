using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace CmdPalCalendar.Feeds;

internal static class LegacyFeedImport
{
    private const string FeedsKey = "CmdPalCalendar.IcsFeeds";

    public static void Run(string settingsPath, CalendarFeedStore store)
    {
        if (!File.Exists(settingsPath) || ReadSettings(settingsPath) is not { } settings ||
            settings[FeedsKey]?.GetValue<string>() is not { } feeds)
        {
            return;
        }

        if (store.Feeds.Count == 0)
        {
            var locations = feeds
                .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Distinct(StringComparer.OrdinalIgnoreCase);
            foreach (var location in locations)
            {
                store.Add(CalendarFeed.Create(CalendarFeed.Describe(location), location, store.NextColor));
            }
        }

        settings.Remove(FeedsKey);
        File.WriteAllText(settingsPath, settings.ToJsonString());
    }

    private static JsonObject? ReadSettings(string path)
    {
        try
        {
            return JsonNode.Parse(File.ReadAllText(path)) as JsonObject;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
