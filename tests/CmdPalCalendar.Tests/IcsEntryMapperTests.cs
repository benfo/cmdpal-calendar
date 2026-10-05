using CmdPalCalendar.Events;
using CmdPalCalendar.Feeds;
using CmdPalCalendar.Ics;
using Ical.Net.CalendarComponents;
using Ical.Net.DataTypes;

namespace CmdPalCalendar.Tests;

public sealed class IcsEntryMapperTests
{
    [Theory]
    [InlineData("https://calendar.google.com/calendar/ical/me/basic.ics", CalendarSite.Google)]
    [InlineData("https://example.com/calendar.ics", null)]
    internal void Events_know_their_calendars_website(string location, CalendarSite? expected)
    {
        var ev = new CalendarEvent { Uid = "1", Summary = "Standup" };
        var period = new Period(new CalDateTime(2026, 10, 5, 9, 0, 0, "UTC"), new CalDateTime(2026, 10, 5, 9, 15, 0, "UTC"));

        var entry = IcsEntryMapper.ToEntry(ev, period, CalendarFeed.Create("Work", new IcsSource(location), CalendarColor.Blue));

        Assert.Equal(expected, entry.Site);
    }
}
