using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CmdPalCalendar.Feeds;
using GraphCalendarModel = Microsoft.Graph.Models.Calendar;

namespace CmdPalCalendar.Graph;

internal sealed record GraphCalendar(string Id, string Name, string? SharedBy, bool IsDefault, CalendarColor? Color);

internal interface IGraphCalendars
{
    Task<IReadOnlyList<GraphCalendar>> GetCalendarsAsync(MicrosoftAccount account, CancellationToken cancellationToken);
}

internal sealed class GraphCalendarList(GraphConnection connection) : IGraphCalendars
{
    private static readonly string[] CalendarFields = ["id", "name", "color", "isDefaultCalendar", "owner"];

    public async Task<IReadOnlyList<GraphCalendar>> GetCalendarsAsync(MicrosoftAccount account, CancellationToken cancellationToken)
    {
        var calendars = new List<GraphCalendar>();
        var client = connection.For(account.Id);
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
}
