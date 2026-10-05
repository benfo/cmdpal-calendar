using System.Globalization;
using CmdPalCalendar.Events;

namespace CmdPalCalendar.Tests;

public sealed class DateTextTests : IDisposable
{
    private static readonly DateOnly Today = new(2026, 10, 5);
    private static readonly DateOnly ThisYear = new(2026, 10, 12);
    private static readonly DateOnly NextYear = new(2027, 3, 10);

    private readonly CultureInfo _culture = CultureInfo.CurrentCulture;

    [Theory]
    [InlineData("en-ZA", "Monday, 12 Oct", "Mon, 12 Oct", "Wednesday, 10 Mar 2027")]
    [InlineData("en-GB", "Monday, 12 Oct", "Mon, 12 Oct", "Wednesday, 10 Mar 2027")]
    [InlineData("en-US", "Monday, Oct 12", "Mon, Oct 12", "Wednesday, Mar 10, 2027")]
    [InlineData("de-DE", "Montag, 12. Okt.", "Mo, 12. Okt.", "Mittwoch, 10. März 2027")]
    [InlineData("fr-FR", "lundi 12 oct.", "lun. 12 oct.", "mercredi 10 mars 2027")]
    [InlineData("ja-JP", "10月12日月曜日", "10月12日月", "2027年3月10日水曜日")]
    public void Follows_the_culture_and_adds_the_year_only_outside_this_year(string culture, string longDate, string shortDate, string otherYear)
    {
        Use(culture);

        Assert.Equal(longDate, DateText.Long(ThisYear, Today));
        Assert.Equal(shortDate, DateText.Short(ThisYear, Today));
        Assert.Equal(otherYear, DateText.Long(NextYear, Today));
    }

    [Fact]
    public void Titles_name_today_tomorrow_and_yesterday()
    {
        Use("en-ZA");

        Assert.Equal("Tomorrow · Tue, 6 Oct", DateText.Title(Today.AddDays(1), Today));
        Assert.Equal("Monday, 12 Oct", DateText.Title(ThisYear, Today));
    }

    [Theory]
    [InlineData("en-ZA", "Mon, 12 Oct – Sun, 18 Oct", "Mon, 8 Mar – Sun, 14 Mar 2027", "Mon, 28 Dec 2026 – Sun, 3 Jan 2027")]
    [InlineData("en-US", "Mon, Oct 12 – Sun, Oct 18", "Mon, Mar 8 – Sun, Mar 14, 2027", "Mon, Dec 28, 2026 – Sun, Jan 3, 2027")]
    public void Spans_show_the_year_on_the_last_date_unless_they_cross_new_year(string culture, string thisYear, string nextYear, string acrossNewYear)
    {
        Use(culture);

        Assert.Equal(thisYear, DateText.Span(new DateOnly(2026, 10, 12), new DateOnly(2026, 10, 18), Today));
        Assert.Equal(nextYear, DateText.Span(new DateOnly(2027, 3, 8), new DateOnly(2027, 3, 14), Today));
        Assert.Equal(acrossNewYear, DateText.Span(new DateOnly(2026, 12, 28), new DateOnly(2027, 1, 3), Today));
    }

    [Theory]
    [InlineData("en-ZA", "March 2027")]
    [InlineData("ja-JP", "2027年3月")]
    public void Names_a_month_with_its_year(string culture, string expected)
    {
        Use(culture);

        Assert.Equal(expected, DateText.Month(new DateOnly(2027, 3, 1)));
    }

    [Theory]
    [InlineData("en-ZA", "14:05")]
    [InlineData("en-US", "2:05 PM")]
    [InlineData("ja-JP", "14:05")]
    public void Clock_times_follow_the_culture(string culture, string expected)
    {
        Use(culture);

        Assert.Equal(expected, TimeText.Clock(new DateTimeOffset(2026, 10, 5, 14, 5, 0, TimeSpan.Zero)));
    }

    public void Dispose() => CultureInfo.CurrentCulture = _culture;

    private static void Use(string culture) => CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
}
