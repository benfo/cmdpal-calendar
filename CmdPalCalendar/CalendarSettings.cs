using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace CmdPalCalendar;

internal sealed partial class CalendarSettings : JsonSettingsManager
{
    private const string Namespace = "CmdPalCalendar";

    private readonly TextSetting _icsFeeds = new(
        $"{Namespace}.{nameof(IcsFeeds)}",
        "ICS feeds",
        "One calendar per line: an https:// or webcal:// ICS URL, or a path to a local .ics file",
        string.Empty)
    {
        Multiline = true,
        Placeholder = "https://outlook.office365.com/owa/calendar/.../calendar.ics",
    };

    public CalendarSettings()
    {
        FilePath = SettingsJsonPath();
        Settings.Add(_icsFeeds);
        LoadSettings();
        Settings.SettingsChanged += (_, _) => SaveSettings();
    }

    public IReadOnlyList<string> IcsFeeds =>
        (_icsFeeds.Value ?? string.Empty)
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    private static string SettingsJsonPath()
    {
        var directory = Utilities.BaseSettingsPath(Namespace);
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, "settings.json");
    }
}
