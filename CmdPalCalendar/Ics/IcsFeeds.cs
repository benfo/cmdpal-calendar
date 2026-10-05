using CmdPalCalendar.Feeds;

namespace CmdPalCalendar.Ics;

internal static class IcsFeeds
{
    public static string IcsLocation(this CalendarFeed feed) => ((IcsSource)feed.Source).Location;
}
