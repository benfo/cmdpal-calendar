using System;
using System.IO;

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
    public string LocationLabel => Describe(Location);

    public static CalendarFeed Create(string name, string location, CalendarColor color) =>
        new(Guid.NewGuid().ToString("N"), name, location, Enabled: true, color);

    public static string Describe(string location)
    {
        if (Uri.TryCreate(location, UriKind.Absolute, out var uri) && !uri.IsFile)
        {
            return uri.Host;
        }

        return Path.GetFileName(location) is { Length: > 0 } file ? file : location;
    }
}
