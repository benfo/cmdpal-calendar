// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Ical.Net;
using Ical.Net.CalendarComponents;
using Ical.Net.DataTypes;

namespace CmdPalCalendar.Calendar;

/// <summary>
/// Loads ICS feeds (URLs or local files) and expands them into today's occurrences.
/// </summary>
internal static partial class IcsCalendarSource
{
    private static readonly HttpClient Http = CreateHttpClient();

    // Multi-day events that started before today still count if they overlap it,
    // so expand recurrences from a little before the start of the day.
    private static readonly TimeSpan LookBehind = TimeSpan.FromDays(14);

    public static async Task<CalendarLoadResult> LoadTodayAsync(IReadOnlyList<string> feeds, CancellationToken ct)
    {
        var dayStart = new DateTimeOffset(DateTime.Today);
        var dayEnd = dayStart.AddDays(1);

        var results = await Task.WhenAll(feeds.Select(f => LoadFeedAsync(f, dayStart, dayEnd, ct)));

        var entries = results
            .SelectMany(r => r.Entries)
            .DistinctBy(e => (e.Uid, e.Start))
            .OrderBy(e => e.Start)
            .ThenBy(e => e.Title, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
        var errors = results.Where(r => r.Error is not null).Select(r => r.Error!).ToList();

        return new CalendarLoadResult(entries, errors, DateTimeOffset.Now);
    }

    private static async Task<(IReadOnlyList<CalendarEntry> Entries, string? Error)> LoadFeedAsync(
        string feed, DateTimeOffset dayStart, DateTimeOffset dayEnd, CancellationToken ct)
    {
        var name = DisplayName(feed);
        try
        {
            var text = await ReadFeedAsync(feed, ct);
            var calendar = Ical.Net.Calendar.Load(text)
                ?? throw new InvalidDataException("Not an iCalendar file");

            var from = new CalDateTime(dayStart.Add(-LookBehind).UtcDateTime);
            var to = new CalDateTime(dayEnd.UtcDateTime);

            var entries = new List<CalendarEntry>();
            foreach (var occurrence in calendar.GetOccurrences<CalendarEvent>(from).TakeWhileBefore(to))
            {
                if (occurrence.Source is not CalendarEvent ev ||
                    string.Equals(ev.Status, "CANCELLED", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var entry = ToEntry(ev, occurrence.Period, name);
                if (entry.Start < dayEnd && entry.End > dayStart)
                {
                    entries.Add(entry);
                }
            }

            return (entries, null);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            return ([], $"{name}: {ex.Message}");
        }
    }

    private static async Task<string> ReadFeedAsync(string feed, CancellationToken ct)
    {
        if (File.Exists(feed))
        {
            return await File.ReadAllTextAsync(feed, ct);
        }

        if (!Uri.TryCreate(feed, UriKind.Absolute, out var uri))
        {
            throw new ArgumentException("Not a URL or an existing file");
        }

        if (uri.IsFile)
        {
            return await File.ReadAllTextAsync(uri.LocalPath, ct);
        }

        // webcal:// is just a hint to calendar apps; the feed itself is served over https.
        if (uri.Scheme is "webcal" or "webcals")
        {
            uri = new UriBuilder(uri) { Scheme = Uri.UriSchemeHttps, Port = -1 }.Uri;
        }

        using var response = await Http.GetAsync(uri, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(ct);
    }

    private static CalendarEntry ToEntry(CalendarEvent ev, Period period, string source)
    {
        var start = period.StartTime;
        var end = period.EffectiveEndTime ?? start;
        var isAllDay = !start.HasTime;

        return new CalendarEntry(
            Uid: ev.Uid ?? Guid.NewGuid().ToString(),
            Title: string.IsNullOrWhiteSpace(ev.Summary) ? "(No title)" : ev.Summary.Trim(),
            Start: ToLocal(start),
            End: ToLocal(end),
            IsAllDay: isAllDay,
            Location: NullIfBlank(ev.Location),
            Description: NullIfBlank(ev.Description),
            Organizer: OrganizerName(ev.Organizer),
            Attendees: ev.Attendees
                .Select(a => NullIfBlank(a.CommonName) ?? MailAddress(a.Value))
                .OfType<string>()
                .ToList(),
            Link: FindLink(ev),
            Source: source);
    }

    // Floating times and all-day dates have no zone: they mean "this wall-clock time wherever you are".
    private static DateTimeOffset ToLocal(CalDateTime value)
    {
        if (value.IsFloating || !value.HasTime)
        {
            var wallClock = DateTime.SpecifyKind(value.Value, DateTimeKind.Unspecified);
            return new DateTimeOffset(wallClock, TimeZoneInfo.Local.GetUtcOffset(wallClock));
        }

        return new DateTimeOffset(value.AsUtc).ToLocalTime();
    }

    private static string? FindLink(CalendarEvent ev)
    {
        // Rough first pass: prefer a known meeting service, then any link. Proper
        // join-link detection (structured fields, unwrapping, native protocols) comes later.
        string?[] sources = [ev.Location, ev.Url?.ToString(), ev.Description];
        var urls = sources
            .Where(s => !string.IsNullOrEmpty(s))
            .SelectMany(s => UrlRegex().Matches(s!).Select(m => m.Value.TrimEnd('.', ',', ';', ')', '>')))
            .ToList();

        return urls.FirstOrDefault(u => MeetingHostRegex().IsMatch(u)) ?? urls.FirstOrDefault();
    }

    private static string? OrganizerName(Organizer? organizer) =>
        organizer is null ? null : NullIfBlank(organizer.CommonName) ?? MailAddress(organizer.Value);

    private static string? MailAddress(Uri? value) =>
        value is null ? null : NullIfBlank(value.OriginalString.Replace("mailto:", string.Empty, StringComparison.OrdinalIgnoreCase));

    private static string? NullIfBlank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string DisplayName(string feed)
    {
        if (File.Exists(feed))
        {
            return Path.GetFileName(feed);
        }

        return Uri.TryCreate(feed, UriKind.Absolute, out var uri) ? uri.Host : feed;
    }

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("CmdPalCalendar/0.1");
        return client;
    }

    [GeneratedRegex(@"https?://[^\s""'<>]+", RegexOptions.IgnoreCase)]
    private static partial Regex UrlRegex();

    [GeneratedRegex(@"^https?://([\w-]+\.)*(teams\.microsoft\.com|teams\.live\.com|zoom\.us|zoom\.com|zoomgov\.com|meet\.google\.com|webex\.com)/", RegexOptions.IgnoreCase)]
    private static partial Regex MeetingHostRegex();
}
