using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CmdPalCalendar.Events;
using Ical.Net;
using Ical.Net.CalendarComponents;
using Ical.Net.DataTypes;

namespace CmdPalCalendar.Ics;

internal sealed class IcsCalendarSource(Func<IReadOnlyList<string>> feeds, IcsFeedReader reader, TimeProvider time)
    : ICalendarSource
{
    private static readonly TimeSpan MaxAge = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan LongestEvent = TimeSpan.FromDays(14);

    private readonly Lock _lock = new();
    private IReadOnlyList<LoadedFeed> _loaded = [];
    private IReadOnlyList<string> _loadedFeeds = [];
    private DateTimeOffset? _loadedAt;

    public bool IsStale =>
        _loadedAt is not { } loadedAt ||
        time.GetUtcNow() - loadedAt > MaxAge ||
        !_loadedFeeds.SequenceEqual(feeds(), StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<string> Errors => _loaded.Select(f => f.Error).OfType<string>().ToList();

    public async Task LoadAsync(CancellationToken cancellationToken)
    {
        var current = feeds().ToArray();
        var loaded = await Task.WhenAll(current.Select(f => LoadFeedAsync(f, cancellationToken)));

        lock (_lock)
        {
            _loaded = loaded;
            _loadedFeeds = current;
            _loadedAt = time.GetUtcNow();
        }
    }

    public IReadOnlyList<CalendarEntry> GetEntries(DateOnly from, DateOnly toExclusive)
    {
        var rangeStart = StartOfDay(from);
        var rangeEnd = StartOfDay(toExclusive);
        var expandFrom = new CalDateTime(rangeStart.Subtract(LongestEvent).UtcDateTime);
        var expandTo = new CalDateTime(rangeEnd.UtcDateTime);

        lock (_lock)
        {
            return _loaded
                .Where(f => f.Calendar is not null)
                .SelectMany(f => f.Calendar!.GetOccurrences<CalendarEvent>(expandFrom)
                    .TakeWhileBefore(expandTo)
                    .Where(o => o.Source is CalendarEvent ev && !IsCancelled(ev))
                    .Select(o => IcsEntryMapper.ToEntry((CalendarEvent)o.Source, o.Period, f.Name)))
                .Where(e => e.Start < rangeEnd && e.End > rangeStart)
                .DistinctBy(e => (e.Uid, e.Start))
                .OrderBy(e => e.Start)
                .ThenBy(e => e.Title, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }
    }

    private async Task<LoadedFeed> LoadFeedAsync(string feed, CancellationToken cancellationToken)
    {
        var name = IcsFeedReader.DisplayName(feed);
        try
        {
            var text = await reader.ReadAsync(feed, cancellationToken);
            var calendar = Calendar.Load(text) ?? throw new InvalidDataException("Not an iCalendar file");
            return new LoadedFeed(name, calendar, null);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            return new LoadedFeed(name, null, $"{name}: {ex.Message}");
        }
    }

    private static DateTimeOffset StartOfDay(DateOnly date)
    {
        var midnight = date.ToDateTime(TimeOnly.MinValue);
        return new DateTimeOffset(midnight, TimeZoneInfo.Local.GetUtcOffset(midnight));
    }

    private static bool IsCancelled(CalendarEvent ev) =>
        string.Equals(ev.Status, "CANCELLED", StringComparison.OrdinalIgnoreCase);

    private sealed record LoadedFeed(string Name, Calendar? Calendar, string? Error);
}
