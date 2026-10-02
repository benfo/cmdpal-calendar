using System;

namespace CmdPalCalendar.Feeds;

internal enum CalendarColor
{
    Blue,
    Purple,
    Green,
    Orange,
    Red,
    Teal,
    Grey,
}

internal sealed record CalendarFeed(string Id, string Name, string Location, bool Enabled, CalendarColor Color)
{
    public static CalendarFeed Create(string name, string location, CalendarColor color) =>
        new(Guid.NewGuid().ToString("N"), name, location, Enabled: true, color);
}
