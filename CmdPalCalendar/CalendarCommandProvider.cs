using System;
using CmdPalCalendar.Ics;
using CmdPalCalendar.Pages;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace CmdPalCalendar;

public partial class CalendarCommandProvider : CommandProvider
{
    private readonly ICommandItem[] _commands;
    private readonly CalendarSettings _settings = new();

    public CalendarCommandProvider()
    {
        Id = "CmdPalCalendar";
        DisplayName = "Calendar";
        Icon = new IconInfo(Glyphs.Calendar);
        Settings = _settings.Settings;

        _commands =
        [
            new CommandItem(new CalendarPage(_settings, CreateSource(_settings), TimeProvider.System))
            {
                Title = "Calendar",
                Subtitle = "Your events, day by day",
                MoreCommands = [new CommandContextItem(_settings.Settings.SettingsPage)],
            },
        ];
    }

    public override ICommandItem[] TopLevelCommands() => _commands;

    private static IcsCalendarSource CreateSource(CalendarSettings settings) =>
        new(() => settings.IcsFeeds, new IcsFeedReader(), TimeProvider.System);
}
