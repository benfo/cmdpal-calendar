using System;
using System.Globalization;

namespace CmdPalCalendar.Pages;

internal static class DateText
{
    public static string Long(DateOnly date, DateOnly today) => Format(date, WithYear("dddd d MMM", date, today));

    public static string Short(DateOnly date, DateOnly today) => Format(date, WithYear("ddd d MMM", date, today));

    public static string Title(DateOnly date, DateOnly today) => (date.DayNumber - today.DayNumber) switch
    {
        0 => $"Today · {Short(date, today)}",
        1 => $"Tomorrow · {Short(date, today)}",
        -1 => $"Yesterday · {Short(date, today)}",
        _ => Long(date, today),
    };

    public static string Relative(DateOnly date, DateOnly today) => (date.DayNumber - today.DayNumber) switch
    {
        0 => "Today",
        1 => "Tomorrow",
        _ => Short(date, today),
    };

    public static string Span(DateOnly first, DateOnly last, DateOnly today) =>
        first == last ? Long(first, today)
        : first.Year != last.Year ? $"{Format(first, "ddd d MMM yyyy")} – {Format(last, "ddd d MMM yyyy")}"
        : first.Month == last.Month ? $"{Format(first, "ddd d")} – {Short(last, today)}"
        : $"{Format(first, "ddd d MMM")} – {Short(last, today)}";

    private static string WithYear(string pattern, DateOnly date, DateOnly today) => date.Year == today.Year ? pattern : $"{pattern} yyyy";

    private static string Format(DateOnly date, string pattern) => date.ToString(pattern, CultureInfo.CurrentCulture);
}
