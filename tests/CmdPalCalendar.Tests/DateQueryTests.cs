using System.Globalization;
using CmdPalCalendar.Events;

namespace CmdPalCalendar.Tests;

public sealed class DateQueryTests
{
    private static readonly DateOnly Friday = new(2026, 10, 2);
    private static readonly CultureInfo British = CultureInfo.GetCultureInfo("en-GB");

    [Theory]
    [InlineData("today", "2026-10-02")]
    [InlineData("tod", "2026-10-02")]
    [InlineData("  Tomorrow ", "2026-10-03")]
    [InlineData("tom", "2026-10-03")]
    [InlineData("yesterday", "2026-10-01")]
    [InlineData("fri", "2026-10-02")]
    [InlineData("mon", "2026-10-05")]
    [InlineData("thursday", "2026-10-08")]
    [InlineData("next fri", "2026-10-09")]
    [InlineData("next mon", "2026-10-05")]
    [InlineData("last fri", "2026-09-25")]
    [InlineData("last wed", "2026-09-30")]
    [InlineData("this week", "2026-09-28")]
    [InlineData("next week", "2026-10-05")]
    [InlineData("last week", "2026-09-21")]
    [InlineData("this weekend", "2026-10-03")]
    [InlineData("next weekend", "2026-10-10")]
    [InlineData("next month", "2026-11-01")]
    [InlineData("last month", "2026-09-01")]
    [InlineData("+3", "2026-10-05")]
    [InlineData("-2", "2026-09-30")]
    [InlineData("in 3 days", "2026-10-05")]
    [InlineData("in a week", "2026-10-09")]
    [InlineData("in two weeks", "2026-10-16")]
    [InlineData("3 days ago", "2026-09-29")]
    [InlineData("in 1 month", "2026-11-02")]
    [InlineData("27th", "2026-10-27")]
    [InlineData("1st", "2026-11-01")]
    [InlineData("27 jan", "2027-01-27")]
    [InlineData("jan 27", "2027-01-27")]
    [InlineData("27 January", "2027-01-27")]
    [InlineData("12 oct", "2026-10-12")]
    [InlineData("1 oct", "2027-10-01")]
    [InlineData("1 oct 2026", "2026-10-01")]
    [InlineData("oct 12th, 2027", "2027-10-12")]
    [InlineData("2026-12-25", "2026-12-25")]
    [InlineData("25/12/2026", "2026-12-25")]
    public void Parses(string text, string expected)
    {
        Assert.True(DateQuery.TryParse(text, Friday, British, out var date));
        Assert.Equal(DateOnly.Parse(expected, CultureInfo.InvariantCulture), date);
    }

    [Theory]
    [InlineData("")]
    [InlineData("standup")]
    [InlineData("monthly review")]
    [InlineData("3")]
    [InlineData("31 feb")]
    [InlineData("next standup")]
    public void Ignores_text_that_is_not_a_date(string text)
    {
        Assert.False(DateQuery.TryParse(text, Friday, British, out _));
    }

    [Fact]
    public void This_weekend_on_a_sunday_is_today()
    {
        var sunday = new DateOnly(2026, 10, 4);

        Assert.True(DateQuery.TryParse("this weekend", sunday, British, out var date));
        Assert.Equal(sunday, date);
    }

    [Fact]
    public void Day_of_month_skips_months_that_are_too_short()
    {
        var february = new DateOnly(2027, 2, 1);

        Assert.True(DateQuery.TryParse("31st", february, British, out var date));
        Assert.Equal(new DateOnly(2027, 3, 31), date);
    }
}
