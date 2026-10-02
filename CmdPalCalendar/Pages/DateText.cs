using System;
using System.Globalization;

namespace CmdPalCalendar.Pages;

internal static class DateText
{
    public static string Long(DateOnly date) => Format(date, "dddd d MMM");

    public static string Short(DateOnly date) => Format(date, "ddd d MMM");

    public static string Title(DateOnly date, DateOnly today) => (date.DayNumber - today.DayNumber) switch
    {
        0 => $"Today · {Short(date)}",
        1 => $"Tomorrow · {Short(date)}",
        -1 => $"Yesterday · {Short(date)}",
        _ => Long(date),
    };

    public static string Relative(DateOnly date, DateOnly today) => (date.DayNumber - today.DayNumber) switch
    {
        0 => "Today",
        1 => "Tomorrow",
        _ => Short(date),
    };

    public static string Span(DateOnly first, DateOnly last) =>
        first == last ? Long(first)
        : first.Month == last.Month ? $"{Format(first, "ddd d")} – {Short(last)}"
        : $"{Short(first)} – {Short(last)}";

    private static string Format(DateOnly date, string pattern) => date.ToString(pattern, CultureInfo.CurrentCulture);
}
