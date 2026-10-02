// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace CmdPalCalendar;

internal sealed partial class SettingsManager : JsonSettingsManager
{
    private static readonly string _namespace = "CmdPalCalendar";

    private static string Namespaced(string propertyName) => $"{_namespace}.{propertyName}";

    private readonly TextSetting _icsFeeds = new(
        Namespaced(nameof(IcsFeeds)),
        "ICS feeds",
        "One calendar per line: an https:// or webcal:// ICS URL, or a path to a local .ics file",
        string.Empty)
    {
        Multiline = true,
        Placeholder = "https://outlook.office365.com/owa/calendar/.../calendar.ics",
    };

    public IReadOnlyList<string> IcsFeeds =>
        (_icsFeeds.Value ?? string.Empty)
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    internal static string SettingsJsonPath()
    {
        var directory = Utilities.BaseSettingsPath("CmdPalCalendar");
        Directory.CreateDirectory(directory);

        return Path.Combine(directory, "settings.json");
    }

    public SettingsManager()
    {
        FilePath = SettingsJsonPath();

        Settings.Add(_icsFeeds);

        // Load settings from file upon initialization
        LoadSettings();

        Settings.SettingsChanged += (_, _) => SaveSettings();
    }
}
