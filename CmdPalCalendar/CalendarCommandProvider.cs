using System;
using System.IO;
using CmdPalCalendar.Events;
using CmdPalCalendar.Feeds;
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
        var reader = new IcsFeedReader();
        var feeds = new CalendarFeedStore(Path.Combine(Utilities.BaseSettingsPath("CmdPalCalendar"), "calendars.json"));
        var manage = new ManageCalendarsPage(feeds, new IcsFeedCheck(reader));
        var source = new IcsCalendarSource(() => _settings.IcsFeeds, reader, time);
        var refresher = new CalendarRefresher(source);
        _settings.Settings.SettingsChanged += (_, _) => refresher.Refresh(force: true);

        var calendar = new CommandItem(new CalendarPage(_settings, source, refresher, time))
        {
            Title = "Calendar",
            Subtitle = "Your events, day by day",
            MoreCommands = [new CommandContextItem(manage), new CommandContextItem(_settings.Settings.SettingsPage)],
        };
        var joinNext = new JoinNextMeetingItem(
            new JoinNextMeetingCommand(source, time),
            new CalendarPage(_settings, source, refresher, time, JoinNextMeetingItem.CommandId));
        _ticker = new NextMeetingTicker(calendar, joinNext, _settings, source, refresher, time);
        _commands = [calendar, joinNext];
    }

    public override ICommandItem[] TopLevelCommands() => _commands;

    public override void Dispose()
    {
        _ticker.Dispose();
        base.Dispose();
    }
}
