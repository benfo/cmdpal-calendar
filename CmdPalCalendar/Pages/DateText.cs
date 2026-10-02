using System;
using System.Globalization;

namespace CmdPalCalendar.Pages;

internal static class DateText
{
    public static string Long(DateOnly date) => date.ToString("dddd d MMM", CultureInfo.CurrentCulture);

    public static string Short(DateOnly date) => date.ToString("ddd d MMM", CultureInfo.CurrentCulture);

    public static string Title(DateOnly date, DateOnly today) => (date.DayNumber - today.DayNumber) switch
    {
        0 => $"Today · {Short(date)}",
        1 => $"Tomorrow · {Short(date)}",
        -1 => $"Yesterday · {Short(date)}",
        _ => Long(date),
    };
}
