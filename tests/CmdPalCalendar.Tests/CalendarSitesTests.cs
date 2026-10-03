using CmdPalCalendar.Events;

namespace CmdPalCalendar.Tests;

public sealed class CalendarSitesTests
{
    [Theory]
    [InlineData("https://outlook.office365.com/owa/calendar/abc/def/calendar.ics", "Outlook")]
    [InlineData("webcal://outlook.office.com/owa/calendar/abc/def/calendar.ics", "Outlook")]
    [InlineData("https://outlook.live.com/owa/calendar/abc/def/calendar.ics", "OutlookCom")]
    [InlineData("https://calendar.google.com/calendar/ical/me%40gmail.com/private-x/basic.ics", "Google")]
    public void Finds_the_website_from_the_calendar_address(string location, string expected)
    {
        Assert.Equal(expected, CalendarSites.Detect(location).ToString());
    }

    [Theory]
    [InlineData("https://example.com/calendar.ics")]
    [InlineData(@"C:\calendars\family.ics")]
    [InlineData("")]
    public void Other_calendars_have_no_website(string location)
    {
        Assert.Null(CalendarSites.Detect(location));
    }

    [Theory]
    [InlineData("Outlook", "https://outlook.office.com/calendar/view/day/2026/10/3")]
    [InlineData("OutlookCom", "https://outlook.live.com/calendar/view/day/2026/10/3")]
    [InlineData("Google", "https://calendar.google.com/calendar/r/day/2026/10/3")]
    public void Opens_the_day(string site, string expected)
    {
        Assert.Equal(expected, CalendarSites.DayAddress(Enum.Parse<CalendarSite>(site), new DateOnly(2026, 10, 3)));
    }
}
