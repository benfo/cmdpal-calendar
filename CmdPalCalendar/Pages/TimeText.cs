using System;
using CmdPalCalendar.Events;

namespace CmdPalCalendar.Pages;

internal static class TimeText
{
    public static string Clock(DateTimeOffset time) => $"{time:HH:mm}";

    public static string Range(CalendarEntry entry) => $"{Clock(entry.Start)}–{Clock(entry.End)}";

    public static string Duration(TimeSpan span)
    {
        var minutes = (int)Math.Round(span.TotalMinutes);
        if (minutes < 1)
        {
            return "less than a minute";
        }

        if (minutes < 60)
        {
            return $"{minutes} min";
        }

        var (hours, rest) = Math.DivRem(minutes, 60);
        return rest == 0 ? $"{hours} h" : $"{hours} h {rest} min";
    }
}
