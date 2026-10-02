using System;
using CmdPalCalendar.Events;
using CmdPalCalendar.Ics;
using CmdPalCalendar.Pages;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace CmdPalCalendar;

public sealed partial class CalendarCommandProvider : CommandProvider
{
    private readonly ICommandItem[] _commands;
    private readonly CalendarSettings _settings = new();
    private readonly NextMeetingTicker _ticker;

    public CalendarCommandProvider()
    {
        Id = "CmdPalCalendar";
        DisplayName = "Calendar";
        Icon = new IconInfo(Glyphs.Calendar);
        Settings = _settings.Settings;

        var time = TimeProvider.System;
        var source = new IcsCalendarSource(() => _settings.IcsFeeds, new IcsFeedReader(), time);
        var refresher = new CalendarRefresher(source);
        _settings.Settings.SettingsChanged += (_, _) => refresher.Refresh(force: true);

        var calendar = new CommandItem(new CalendarPage(_settings, source, refresher, time))
        {
            Title = "Calendar",
            Subtitle = "Your events, day by day",
            MoreCommands = [new CommandContextItem(_settings.Settings.SettingsPage)],
        };
        _ticker = new NextMeetingTicker(calendar, _settings, source, refresher, time);
        _commands = [calendar];
    }

    public override ICommandItem[] TopLevelCommands() => _commands;

    public override void Dispose()
    {
        _ticker.Dispose();
        base.Dispose();
    }
}
