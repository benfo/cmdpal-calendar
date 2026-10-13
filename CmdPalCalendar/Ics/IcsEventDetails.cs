using System;
using System.Collections.Generic;
using System.Linq;
using Ical.Net.CalendarComponents;

namespace CmdPalCalendar.Ics;

internal sealed record IcsEventDetails(
    string? Uid,
    string? Summary,
    string? Location,
    string? Description,
    string? HtmlDescription,
    string? Url,
    string? Organizer,
    IReadOnlyList<string> Attendees)
{
    public static IcsEventDetails From(CalendarEvent ev) =>
        new(
            ev.Uid,
            ev.Summary,
            ev.Location,
            ev.Description,
            ev.Properties.Get<string>("X-ALT-DESC"),
            ev.Url?.ToString(),
            ev.Organizer is { } organizer ? NameOrAddress(organizer.CommonName, organizer.Value) : null,
            ev.Attendees.Select(a => NameOrAddress(a.CommonName, a.Value)).OfType<string>().ToArray());

    private static string? NameOrAddress(string? name, Uri? address) =>
        IcsText.NullIfBlank(name) ?? IcsText.NullIfBlank(address?.OriginalString.Replace("mailto:", string.Empty, StringComparison.OrdinalIgnoreCase));
}
