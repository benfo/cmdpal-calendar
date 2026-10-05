using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace CmdPalCalendar.Events;

internal static partial class DateText
{
    public static string Long(DateOnly date, DateOnly today) => Format(date, Pattern(fullWeekday: true, withYear: date.Year != today.Year));

    public static string Short(DateOnly date, DateOnly today) => Format(date, Pattern(fullWeekday: false, withYear: date.Year != today.Year));

    public static string Month(DateOnly month) => Format(month, CultureInfo.CurrentCulture.DateTimeFormat.YearMonthPattern);

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
        first == last
            ? Long(first, today)
            : $"{Format(first, Pattern(fullWeekday: false, withYear: first.Year != last.Year))} – {Short(last, today)}";

    private static string Pattern(bool fullWeekday, bool withYear)
    {
        var tokens = TokenRegex().Matches(CultureInfo.CurrentCulture.DateTimeFormat.LongDatePattern).Select(m => m.Value).ToList();
        if (!withYear)
        {
            RemoveYear(tokens);
        }

        return string.Concat(tokens.Select(t => t switch
        {
            "MMMM" => "MMM",
            "dd" => "d",
            "dddd" when !fullWeekday => "ddd",
            _ => t,
        }));
    }

    private static void RemoveYear(List<string> tokens)
    {
        var year = tokens.FindIndex(t => t[0] == 'y');
        if (year < 0)
        {
            return;
        }

        var separator = year > 0 && IsLiteral(tokens[year - 1]) ? year - 1
            : year + 1 < tokens.Count && IsLiteral(tokens[year + 1]) ? year + 1
            : -1;
        tokens.RemoveAt(year);
        if (separator >= 0)
        {
            tokens.RemoveAt(separator > year ? separator - 1 : separator);
        }
    }

    private static bool IsLiteral(string token) => !FieldRegex().IsMatch(token);

    private static string Format(DateOnly date, string pattern) => date.ToString(pattern, CultureInfo.CurrentCulture);

    [GeneratedRegex(@"([dMyg])\1*|(?:'[^']*'|\\.|[^dMyg'\\])+")]
    private static partial Regex TokenRegex();

    [GeneratedRegex(@"^([dMyg])\1*$")]
    private static partial Regex FieldRegex();
}
