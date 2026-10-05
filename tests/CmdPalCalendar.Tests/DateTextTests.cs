using System.Globalization;
using CmdPalCalendar.Pages;

namespace CmdPalCalendar.Tests;

public sealed class DateTextTests : IDisposable
{
    private static readonly DateOnly Today = new(2026, 10, 5);

    private readonly CultureInfo _culture = CultureInfo.CurrentCulture;

    public DateTextTests() => CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-GB");

    [Fact]
    public void Leaves_the_year_out_this_year()
    {
        Assert.Equal("Monday 12 Oct", DateText.Long(new DateOnly(2026, 10, 12), Today));
        Assert.Equal("Tomorrow · Tue 6 Oct", DateText.Title(Today.AddDays(1), Today));
        Assert.Equal("Mon 12 – Sun 18 Oct", DateText.Span(new DateOnly(2026, 10, 12), new DateOnly(2026, 10, 18), Today));
    }

    [Fact]
    public void Adds_the_year_in_another_year()
    {
        Assert.Equal("Wednesday 10 Mar 2027", DateText.Long(new DateOnly(2027, 3, 10), Today));
        Assert.Equal("Fri 3 Oct 2025", DateText.Short(new DateOnly(2025, 10, 3), Today));
        Assert.Equal("Wednesday 10 Mar 2027", DateText.Title(new DateOnly(2027, 3, 10), Today));
    }

    [Fact]
    public void Shows_the_year_once_for_a_week_in_another_year()
    {
        Assert.Equal("Mon 8 – Sun 14 Mar 2027", DateText.Span(new DateOnly(2027, 3, 8), new DateOnly(2027, 3, 14), Today));
        Assert.Equal("Mon 29 Mar – Sun 4 Apr 2027", DateText.Span(new DateOnly(2027, 3, 29), new DateOnly(2027, 4, 4), Today));
    }

    [Fact]
    public void Shows_both_years_for_a_week_across_new_year()
    {
        Assert.Equal("Mon 28 Dec 2026 – Sun 3 Jan 2027", DateText.Span(new DateOnly(2026, 12, 28), new DateOnly(2027, 1, 3), Today));
    }

    public void Dispose() => CultureInfo.CurrentCulture = _culture;
}
