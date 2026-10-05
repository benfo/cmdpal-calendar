using CmdPalCalendar.Feeds;
using OutlookColor = Microsoft.Graph.Models.CalendarColor;

namespace CmdPalCalendar.Graph;

internal static class GraphColors
{
    public static CalendarColor? ToCalendarColor(OutlookColor? color) => color switch
    {
        OutlookColor.LightBlue => CalendarColor.Blue,
        OutlookColor.LightGreen => CalendarColor.Green,
        OutlookColor.LightOrange or OutlookColor.LightYellow or OutlookColor.LightBrown => CalendarColor.Orange,
        OutlookColor.LightGray => CalendarColor.Grey,
        OutlookColor.LightTeal => CalendarColor.Teal,
        OutlookColor.LightPink => CalendarColor.Purple,
        OutlookColor.LightRed => CalendarColor.Red,
        _ => null,
    };
}
