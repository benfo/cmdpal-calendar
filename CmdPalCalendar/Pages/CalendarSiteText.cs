using CmdPalCalendar.Events;

namespace CmdPalCalendar.Pages;

internal static class CalendarSiteText
{
    public static string Name(CalendarSite site) => site switch
    {
        CalendarSite.Outlook => "Outlook",
        CalendarSite.OutlookCom => "Outlook.com",
        _ => "Google Calendar",
    };
}
