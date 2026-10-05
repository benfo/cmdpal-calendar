using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CmdPalCalendar.Events;
using CmdPalCalendar.Feeds;
using Microsoft.Graph.Me.Calendars.Item.CalendarView.Delta;
using Microsoft.Graph.Models.ODataErrors;
using GraphEvent = Microsoft.Graph.Models.Event;

namespace CmdPalCalendar.Graph;

internal sealed record GraphChanges(IReadOnlyList<CalendarEntry> Changed, IReadOnlySet<string> Removed, string? DeltaLink, bool IsFull)
{
    public IReadOnlyList<CalendarEntry> ApplyTo(IReadOnlyList<CalendarEntry> entries)
    {
        if (IsFull)
        {
            return Changed;
        }

        var changedIds = Changed.Select(e => e.Uid).ToHashSet();
        return [.. entries.Where(e => !Removed.Contains(e.Uid) && !changedIds.Contains(e.Uid)), .. Changed];
    }
}

internal interface IGraphEvents
{
    Task<IReadOnlyList<CalendarEntry>> GetEntriesAsync(CalendarFeed feed, DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken);

    Task<GraphChanges> SyncAsync(CalendarFeed feed, DateTimeOffset from, DateTimeOffset to, string? deltaLink, CancellationToken cancellationToken);
}

internal sealed class GraphEventReader(GraphConnection connection) : IGraphEvents
{
    private const int PageSize = 100;

    private static readonly string[] EventFields =
        ["id", "subject", "start", "end", "isAllDay", "isCancelled", "location", "body", "organizer", "attendees", "onlineMeeting", "webLink"];

    public async Task<IReadOnlyList<CalendarEntry>> GetEntriesAsync(CalendarFeed feed, DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken)
    {
        var source = (MicrosoftSource)feed.Source;
        var calendarView = connection.For(source.AccountId).Me.Calendars[source.CalendarId].CalendarView;
        var events = new List<GraphEvent>();
        var page = await calendarView.GetAsync(
            r =>
            {
                r.QueryParameters.StartDateTime = Iso(from);
                r.QueryParameters.EndDateTime = Iso(to);
                r.QueryParameters.Select = EventFields;
                r.QueryParameters.Top = PageSize;
                GraphConnection.PreferUtcAndHtml(r.Headers);
            },
            cancellationToken);
        while (page is not null)
        {
            events.AddRange(page.Value ?? []);
            page = page.OdataNextLink is { } next
                ? await calendarView.WithUrl(next).GetAsync(r => GraphConnection.PreferUtcAndHtml(r.Headers), cancellationToken)
                : null;
        }

        return events.Where(e => e.IsCancelled != true).Select(e => GraphEntryMapper.ToEntry(e, feed)).ToList();
    }

    public async Task<GraphChanges> SyncAsync(CalendarFeed feed, DateTimeOffset from, DateTimeOffset to, string? deltaLink, CancellationToken cancellationToken)
    {
        var delta = Delta(feed);
        if (deltaLink is not null)
        {
            try
            {
                return await ReadDeltaAsync(feed, delta.WithUrl(deltaLink), null, isFull: false, cancellationToken);
            }
            catch (ODataError ex) when (ex.ResponseStatusCode == 410)
            {
            }
        }

        try
        {
            return await ReadDeltaAsync(feed, delta, (Iso(from), Iso(to)), isFull: true, cancellationToken);
        }
        catch (ODataError ex) when (ex.ResponseStatusCode is 400 or 501)
        {
            var entries = await GetEntriesAsync(feed, from, to, cancellationToken);
            return new GraphChanges(entries, new HashSet<string>(), null, IsFull: true);
        }
    }

    private async Task<GraphChanges> ReadDeltaAsync(
        CalendarFeed feed, DeltaRequestBuilder delta, (string From, string To)? window, bool isFull, CancellationToken cancellationToken)
    {
        var changed = new Dictionary<string, CalendarEntry>();
        var removed = new HashSet<string>();
        var page = await delta.GetAsDeltaGetResponseAsync(
            r =>
            {
                if (window is { } range)
                {
                    r.QueryParameters.StartDateTime = range.From;
                    r.QueryParameters.EndDateTime = range.To;
                }

                PreferPaged(r.Headers);
            },
            cancellationToken);

        while (true)
        {
            foreach (var ev in page?.Value ?? [])
            {
                if (ev.Id is not { } id)
                {
                    continue;
                }

                if (ev.AdditionalData.ContainsKey("@removed") || ev.IsCancelled == true)
                {
                    changed.Remove(id);
                    removed.Add(id);
                }
                else
                {
                    removed.Remove(id);
                    changed[id] = GraphEntryMapper.ToEntry(ev, feed);
                }
            }

            if (page?.OdataNextLink is not { } next)
            {
                return new GraphChanges(changed.Values.ToList(), removed, page?.OdataDeltaLink, isFull);
            }

            page = await delta.WithUrl(next).GetAsDeltaGetResponseAsync(r => PreferPaged(r.Headers), cancellationToken);
        }
    }

    private DeltaRequestBuilder Delta(CalendarFeed feed)
    {
        var source = (MicrosoftSource)feed.Source;
        return connection.For(source.AccountId).Me.Calendars[source.CalendarId].CalendarView.Delta;
    }

    private static void PreferPaged(Microsoft.Kiota.Abstractions.RequestHeaders headers)
    {
        GraphConnection.PreferUtcAndHtml(headers);
        headers.Add("Prefer", $"odata.maxpagesize={PageSize}");
    }

    private static string Iso(DateTimeOffset time) => time.UtcDateTime.ToString("o", CultureInfo.InvariantCulture);
}
