using System;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Windows.System;

namespace CmdPalCalendar.Pages;

internal sealed class NavigationCommands
{
    private readonly AnonymousCommand _previous;
    private readonly AnonymousCommand _next;

    public NavigationCommands(Action previous, Action next, Action today, Action refresh)
    {
        _previous = Command(previous, Glyphs.Previous);
        _next = Command(next, Glyphs.Next);
        StepBy("day");

        Items =
        [
            ContextItem(_previous, VirtualKey.Left),
            ContextItem(_next, VirtualKey.Right),
            ContextItem(Command(today, Glyphs.CalendarDay, "Today"), VirtualKey.T),
            ContextItem(Command(refresh, Glyphs.Refresh, "Refresh"), VirtualKey.R),
        ];
    }

    public IContextItem[] Items { get; }

    public void StepBy(string unit)
    {
        _previous.Name = $"Previous {unit}";
        _next.Name = $"Next {unit}";
    }

    private static AnonymousCommand Command(Action action, string glyph, string name = "") =>
        new(action) { Name = name, Icon = new IconInfo(glyph), Result = CommandResult.KeepOpen() };

    private static CommandContextItem ContextItem(ICommand command, VirtualKey key) =>
        new(command) { RequestedShortcut = KeyChordHelpers.FromModifiers(ctrl: true, vkey: key) };
}
