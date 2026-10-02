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
        Icon = IconHelpers.FromRelativePath("Assets\\StoreLogo.png");
        Settings = _settings.Settings;

        _commands =
        [
            new CommandItem(new CalendarPage(_settings))
            {
                Title = "Today's calendar",
                Subtitle = "Your events for the rest of today",
                MoreCommands = [new CommandContextItem(_settings.Settings.SettingsPage)],
            },
        ];
    }

    public override ICommandItem[] TopLevelCommands() => _commands;
}
