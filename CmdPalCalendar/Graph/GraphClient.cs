using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using CmdPalCalendar.Events;
using CmdPalCalendar.Feeds;
using Microsoft.Graph;
using GraphEvent = Microsoft.Graph.Models.Event;
using Microsoft.Kiota.Abstractions;
using Microsoft.Kiota.Abstractions.Authentication;
using GraphCalendarModel = Microsoft.Graph.Models.Calendar;

namespace CmdPalCalendar.Graph;

internal sealed record GraphCalendar(string Id, string Name, string? SharedBy, bool IsDefault, CalendarColor? Color);

internal interface IGraphCalendars
{
    Task<IReadOnlyList<GraphCalendar>> GetCalendarsAsync(MicrosoftAccount account, CancellationToken cancellationToken);
}

internal interface IGraphEvents
{
    Task<IReadOnlyList<CalendarEntry>> GetEntriesAsync(CalendarFeed feed, DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken);
}

internal sealed class GraphClient(IMicrosoftSignIn signIn, HttpClient http) : IGraphCalendars, IGraphEvents
{
    private static readonly string[] CalendarFields = ["id", "name", "color", "isDefaultCalendar", "owner"];

    private static readonly string[] EventFields =
        ["id", "subject", "start", "end", "isAllDay", "isCancelled", "location", "body", "organizer", "attendees", "onlineMeeting", "webLink"];

    public async Task<IReadOnlyList<CalendarEntry>> GetEntriesAsync(CalendarFeed feed, DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken)
    {
        var source = (MicrosoftSource)feed.Source;
        var calendarView = Client(source.AccountId).Me.Calendars[source.CalendarId].CalendarView;
        var events = new List<GraphEvent>();
        var page = await calendarView.GetAsync(
            r =>
            {
                r.QueryParameters.StartDateTime = from.UtcDateTime.ToString("o", CultureInfo.InvariantCulture);
                r.QueryParameters.EndDateTime = to.UtcDateTime.ToString("o", CultureInfo.InvariantCulture);
                r.QueryParameters.Select = EventFields;
                r.QueryParameters.Top = 100;
                Prefer(r.Headers);
            },
            cancellationToken);
        while (page is not null)
        {
            events.AddRange(page.Value ?? []);
            page = page.OdataNextLink is { } next
                ? await calendarView.WithUrl(next).GetAsync(r => Prefer(r.Headers), cancellationToken)
                : null;
        }

        return events.Where(e => e.IsCancelled != true).Select(e => GraphEntryMapper.ToEntry(e, feed)).ToList();
    }

    public async Task<IReadOnlyList<GraphCalendar>> GetCalendarsAsync(MicrosoftAccount account, CancellationToken cancellationToken)
    {
        var calendars = new List<GraphCalendar>();
        var client = Client(account.Id);
        var page = await client.Me.Calendars.GetAsync(r => r.QueryParameters.Select = CalendarFields, cancellationToken);
        while (page is not null)
        {
            calendars.AddRange((page.Value ?? []).Select(c => ToCalendar(c, account)));
            page = page.OdataNextLink is { } next
                ? await client.Me.Calendars.WithUrl(next).GetAsync(cancellationToken: cancellationToken)
                : null;
        }

        return calendars.OrderByDescending(c => c.IsDefault).ThenBy(c => c.SharedBy is not null).ToList();
    }

    private static GraphCalendar ToCalendar(GraphCalendarModel calendar, MicrosoftAccount account)
    {
        var owner = calendar.Owner;
        var sharedBy = owner?.Address is { } address && !string.Equals(address, account.Username, StringComparison.OrdinalIgnoreCase)
            ? owner.Name ?? address
            : null;
        return new GraphCalendar(calendar.Id ?? string.Empty, calendar.Name ?? "(No name)", sharedBy, calendar.IsDefaultCalendar ?? false, GraphColors.ToCalendarColor(calendar.Color));
    }

    private static void Prefer(RequestHeaders headers)
    {
        headers.Add("Prefer", "outlook.timezone=\"UTC\"");
        headers.Add("Prefer", "outlook.body-content-type=\"html\"");
    }

    private GraphServiceClient Client(string accountId) =>
        new(http, new BaseBearerTokenAuthenticationProvider(new TokenProvider(signIn, accountId)));

    private sealed class TokenProvider(IMicrosoftSignIn signIn, string accountId) : IAccessTokenProvider
    {
        public AllowedHostsValidator AllowedHostsValidator { get; } = new(["graph.microsoft.com"]);

        public Task<string> GetAuthorizationTokenAsync(Uri uri, Dictionary<string, object>? additionalAuthenticationContext = null, CancellationToken cancellationToken = default) =>
            signIn.GetTokenAsync(accountId, cancellationToken);
    }
}
