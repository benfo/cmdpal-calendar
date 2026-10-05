using System;
using System.Globalization;

namespace CmdPalCalendar.Events;

internal enum CalendarSite
{
    Outlook,
    OutlookCom,
    Google,
}

internal static class CalendarSites
{
    public static CalendarSite? Detect(string calendarLocation)
    {
        if (!Uri.TryCreate(calendarLocation, UriKind.Absolute, out var uri) || uri.IsFile)
        {
            return null;
        }

        return uri.Host.ToUpperInvariant() switch
        {
            "OUTLOOK.OFFICE365.COM" or "OUTLOOK.OFFICE.COM" => CalendarSite.Outlook,
            "OUTLOOK.LIVE.COM" => CalendarSite.OutlookCom,
            "CALENDAR.GOOGLE.COM" => CalendarSite.Google,
            _ => null,
        };
    }

    public static string Address(CalendarSite site, DateOnly day) => site switch
    {
        CalendarSite.Outlook => "https://outlook.office.com/calendar/view/day",
        CalendarSite.OutlookCom => "https://outlook.live.com/calendar/view/day",
        _ => $"https://calendar.google.com/calendar/r/day/{day.ToString("yyyy/M/d", CultureInfo.InvariantCulture)}",
    };
}
