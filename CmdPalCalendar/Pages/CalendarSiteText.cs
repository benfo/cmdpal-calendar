using CmdPalCalendar.Events;

namespace CmdPalCalendar.Pages;

internal static class CalendarSiteText
{
    public static string OpenLabel(CalendarSite site) => site switch
    {
        CalendarSite.Outlook => "Open Outlook calendar",
        CalendarSite.OutlookCom => "Open Outlook.com calendar",
        _ => "Open in Google Calendar",
    };

    public static string OpenEventLabel(CalendarSite? site) => site switch
    {
        CalendarSite.Outlook => "Open in Outlook",
        CalendarSite.OutlookCom => "Open in Outlook.com",
        CalendarSite.Google => "Open in Google Calendar",
        _ => "Open event",
    };
}
