using System;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Windows.System;

namespace CmdPalCalendar.Pages;

internal sealed class NavigationCommands(Action previous, Action next, Action today, Action refresh)
{
    public IContextItem[] Items { get; } =
    [
        Create("Previous day", "", VirtualKey.Left, previous),
        Create("Next day", "", VirtualKey.Right, next),
        Create("Today", "", VirtualKey.T, today),
        Create("Refresh", "", VirtualKey.R, refresh),
    ];

    private static CommandContextItem Create(string name, string glyph, VirtualKey key, Action action) =>
        new(new AnonymousCommand(action) { Name = name, Icon = new IconInfo(glyph), Result = CommandResult.KeepOpen() })
        {
            RequestedShortcut = KeyChordHelpers.FromModifiers(ctrl: true, vkey: key),
        };
}
