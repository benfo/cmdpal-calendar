using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace CmdPalCalendar.Events;

internal static partial class DateQuery
{
    private static readonly Dictionary<string, DayOfWeek> Weekdays = new(StringComparer.Ordinal)
    {
        ["sun"] = DayOfWeek.Sunday, ["sunday"] = DayOfWeek.Sunday,
        ["mon"] = DayOfWeek.Monday, ["monday"] = DayOfWeek.Monday,
        ["tue"] = DayOfWeek.Tuesday, ["tues"] = DayOfWeek.Tuesday, ["tuesday"] = DayOfWeek.Tuesday,
        ["wed"] = DayOfWeek.Wednesday, ["wednesday"] = DayOfWeek.Wednesday,
        ["thu"] = DayOfWeek.Thursday, ["thur"] = DayOfWeek.Thursday, ["thurs"] = DayOfWeek.Thursday, ["thursday"] = DayOfWeek.Thursday,
        ["fri"] = DayOfWeek.Friday, ["friday"] = DayOfWeek.Friday,
        ["sat"] = DayOfWeek.Saturday, ["saturday"] = DayOfWeek.Saturday,
    };

    private static readonly string[] MonthNames =
        ["jan", "feb", "mar", "apr", "may", "jun", "jul", "aug", "sep", "oct", "nov", "dec"];

    private static readonly Dictionary<string, int> NumberWords = new(StringComparer.Ordinal)
    {
        ["a"] = 1, ["an"] = 1, ["one"] = 1, ["two"] = 2, ["three"] = 3, ["four"] = 4, ["five"] = 5,
        ["six"] = 6, ["seven"] = 7, ["eight"] = 8, ["nine"] = 9, ["ten"] = 10,
    };

    public static bool TryParse(string text, DateOnly today, out DateOnly date) =>
        TryParse(text, today, CultureInfo.CurrentCulture, out date);

    public static bool TryParse(string text, DateOnly today, CultureInfo culture, out DateOnly date)
    {
        var query = WhitespaceRegex().Replace(text.Trim().ToLowerInvariant(), " ");
        var firstDayOfWeek = culture.DateTimeFormat.FirstDayOfWeek;

        DateOnly? parsed = query switch
        {
            "" => null,
            "today" or "tod" => today,
            "tomorrow" or "tom" => today.AddDays(1),
            "yesterday" => today.AddDays(-1),
            "this week" => StartOfWeek(today, firstDayOfWeek),
            "next week" => StartOfWeek(today, firstDayOfWeek).AddDays(7),
            "last week" => StartOfWeek(today, firstDayOfWeek).AddDays(-7),
            "this weekend" => Max(today, WeekendSaturday(today)),
            "next weekend" => WeekendSaturday(today).AddDays(7),
            "next month" => new DateOnly(today.Year, today.Month, 1).AddMonths(1),
            "last month" => new DateOnly(today.Year, today.Month, 1).AddMonths(-1),
            _ => Weekday(query, today, firstDayOfWeek)
                ?? Offset(query, today)
                ?? DayOfMonth(query, today)
                ?? MonthAndDay(query, today)
                ?? Formatted(query, culture),
        };

        date = parsed ?? default;
        return parsed is not null;
    }

    private static DateOnly? Weekday(string query, DateOnly today, DayOfWeek firstDayOfWeek)
    {
        var (modifier, name) = query.Split(' ') switch
        {
            [var day] => (string.Empty, day),
            [var direction, var day] when direction is "next" or "last" => (direction, day),
            _ => (string.Empty, string.Empty),
        };

        if (!Weekdays.TryGetValue(name, out var weekday))
        {
            return null;
        }

        var coming = Coming(today, weekday);
        return modifier switch
        {
            "next" => StartOfWeek(coming, firstDayOfWeek) == StartOfWeek(today, firstDayOfWeek) ? coming.AddDays(7) : coming,
            "last" => coming == today ? today.AddDays(-7) : coming.AddDays(-7),
            _ => coming,
        };
    }

    private static DateOnly? Offset(string query, DateOnly today)
    {
        var match = OffsetRegex().Match(query);
        if (!match.Success)
        {
            return null;
        }

        if (match.Groups["sign"].Success)
        {
            var days = int.Parse(match.Groups["signed"].Value, CultureInfo.InvariantCulture);
            return today.AddDays(match.Groups["sign"].Value == "-" ? -days : days);
        }

        if (!TryNumber(match.Groups["count"].Value, out var count))
        {
            return null;
        }

        var direction = match.Groups["ago"].Success ? -1 : 1;
        return match.Groups["unit"].Value switch
        {
            var u when u.StartsWith("day", StringComparison.Ordinal) => today.AddDays(direction * count),
            var u when u.StartsWith("week", StringComparison.Ordinal) => today.AddDays(direction * count * 7),
            _ => today.AddMonths(direction * count),
        };
    }

    private static DateOnly? DayOfMonth(string query, DateOnly today)
    {
        var match = OrdinalDayRegex().Match(query);
        if (!match.Success)
        {
            return null;
        }

        var day = int.Parse(match.Groups["day"].Value, CultureInfo.InvariantCulture);
        for (var month = new DateOnly(today.Year, today.Month, 1); month.Year <= today.Year + 1; month = month.AddMonths(1))
        {
            if (day <= DateTime.DaysInMonth(month.Year, month.Month) && month.AddDays(day - 1) >= today)
            {
                return month.AddDays(day - 1);
            }
        }

        return null;
    }

    private static DateOnly? MonthAndDay(string query, DateOnly today)
    {
        var match = DayMonthRegex().Match(query);
        if (!match.Success)
        {
            match = MonthDayRegex().Match(query);
        }

        if (!match.Success)
        {
            return null;
        }

        var month = Array.FindIndex(MonthNames, m => match.Groups["month"].Value.StartsWith(m, StringComparison.Ordinal)) + 1;
        var day = int.Parse(match.Groups["day"].Value, CultureInfo.InvariantCulture);

        if (match.Groups["year"].Success)
        {
            return TryCreate(int.Parse(match.Groups["year"].Value, CultureInfo.InvariantCulture), month, day);
        }

        var thisYear = TryCreate(today.Year, month, day);
        return thisYear >= today ? thisYear : TryCreate(today.Year + 1, month, day);
    }

    private static DateOnly? Formatted(string query, CultureInfo culture)
    {
        if (!query.Any(char.IsDigit) || !query.Any(c => c is '-' or '/' or '.'))
        {
            return null;
        }

        if (DateOnly.TryParseExact(query, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var iso))
        {
            return iso;
        }

        return DateOnly.TryParse(query, culture, DateTimeStyles.None, out var local) ? local : null;
    }

    private static bool TryNumber(string text, out int number) =>
        int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out number) || NumberWords.TryGetValue(text, out number);

    private static DateOnly? TryCreate(int year, int month, int day) =>
        month is >= 1 and <= 12 && day >= 1 && day <= DateTime.DaysInMonth(year, month) ? new DateOnly(year, month, day) : null;

    private static DateOnly Coming(DateOnly from, DayOfWeek weekday) =>
        from.AddDays(((int)weekday - (int)from.DayOfWeek + 7) % 7);

    private static DateOnly StartOfWeek(DateOnly date, DayOfWeek firstDayOfWeek) =>
        date.AddDays(-(((int)date.DayOfWeek - (int)firstDayOfWeek + 7) % 7));

    private static DateOnly WeekendSaturday(DateOnly today) =>
        today.DayOfWeek == DayOfWeek.Sunday ? today.AddDays(-1) : Coming(today, DayOfWeek.Saturday);

    private static DateOnly Max(DateOnly a, DateOnly b) => a > b ? a : b;

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();

    [GeneratedRegex(@"^(?:(?<sign>[+-])(?<signed>\d{1,4})|(?:in )?(?<count>\d{1,4}|[a-z]+) (?<unit>days?|weeks?|months?)(?<ago> ago)?)$")]
    private static partial Regex OffsetRegex();

    [GeneratedRegex(@"^(?:the )?(?<day>\d{1,2})(?:st|nd|rd|th)$")]
    private static partial Regex OrdinalDayRegex();

    [GeneratedRegex(@"^(?<day>\d{1,2})(?:st|nd|rd|th)? (?<month>jan|feb|mar|apr|may|jun|jul|aug|sep|oct|nov|dec)[a-z]*(?: (?<year>\d{4}))?$")]
    private static partial Regex DayMonthRegex();

    [GeneratedRegex(@"^(?<month>jan|feb|mar|apr|may|jun|jul|aug|sep|oct|nov|dec)[a-z]* (?<day>\d{1,2})(?:st|nd|rd|th)?(?:,? (?<year>\d{4}))?$")]
    private static partial Regex MonthDayRegex();
}
