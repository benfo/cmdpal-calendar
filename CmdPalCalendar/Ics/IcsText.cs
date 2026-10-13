namespace CmdPalCalendar.Ics;

internal static class IcsText
{
    public static string? NullIfBlank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
