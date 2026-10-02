using CmdPalCalendar.Feeds;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace CmdPalCalendar.Pages;

internal static class CalendarColors
{
    public static OptionalColor Background(CalendarColor color) => color switch
    {
        CalendarColor.Blue => ColorHelpers.FromRgb(0x0F, 0x6C, 0xBD),
        CalendarColor.Purple => ColorHelpers.FromRgb(0x87, 0x64, 0xB8),
        CalendarColor.Green => ColorHelpers.FromRgb(0x10, 0x7C, 0x10),
        CalendarColor.Orange => ColorHelpers.FromRgb(0xCA, 0x50, 0x10),
        CalendarColor.Red => ColorHelpers.FromRgb(0xD1, 0x34, 0x38),
        CalendarColor.Teal => ColorHelpers.FromRgb(0x03, 0x83, 0x87),
        _ => ColorHelpers.FromRgb(0x69, 0x79, 0x7E),
    };

    public static Tag Tag(string text, CalendarColor color) =>
        new(text) { Background = Background(color), Foreground = ColorHelpers.FromRgb(0xFF, 0xFF, 0xFF) };
}
