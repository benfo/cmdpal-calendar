using System;

namespace CmdPalCalendar.Events;

internal static class LocalTime
{
    public static DateTimeOffset FromWallClock(DateTime wallClock)
    {
        var unspecified = DateTime.SpecifyKind(wallClock, DateTimeKind.Unspecified);
        return new DateTimeOffset(unspecified, TimeZoneInfo.Local.GetUtcOffset(unspecified));
    }

    public static DateTimeOffset StartOfDay(DateOnly date) => FromWallClock(date.ToDateTime(TimeOnly.MinValue));
}
