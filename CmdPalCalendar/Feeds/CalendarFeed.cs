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

internal sealed record CalendarFeed(string Id, string Name, FeedSource Source, bool Enabled, CalendarColor Color)
{
    public static CalendarFeed Create(string name, FeedSource source, CalendarColor color) =>
        new(Guid.NewGuid().ToString("N"), name, source, Enabled: true, color);
}
