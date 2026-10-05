using System;
using System.IO;
using CmdPalCalendar.Events;
using CmdPalCalendar.Feeds;
using CmdPalCalendar.Graph;
using CmdPalCalendar.Ics;
using CmdPalCalendar.Pages;
using CmdPalCalendar.Platform;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.Graph;

namespace CmdPalCalendar;

public sealed partial class CalendarCommandProvider : CommandProvider
{
    private readonly ICommandItem[] _commands;
    private readonly NextMeetingTicker _ticker;

    public CalendarCommandProvider()
    {
        Id = "CmdPalCalendar";
        DisplayName = "Calendar";
        Icon = new IconInfo(Glyphs.Calendar);

        var directory = Utilities.BaseSettingsPath("CmdPalCalendar");
        var protector = new DpapiSecretProtector();
        var feeds = new CalendarFeedStore(Path.Combine(directory, "calendars.json"), protector);
        LegacyFeedImport.Run(Path.Combine(directory, "settings.json"), feeds);

        var time = TimeProvider.System;
        var reader = new IcsFeedReader();
        var cache = new IcsFeedCache(Path.Combine(directory, "cache"), protector, time);
        var signIn = new WamSignIn();
        var graph = new GraphClient(signIn, GraphClientFactory.Create());
        var source = new CompositeCalendarSource(
            new IcsCalendarSource(() => feeds.EnabledFeeds, reader, cache, time),
            new GraphCalendarSource(() => feeds.EnabledFeeds, graph, time));
        var refresher = new CalendarRefresher(source);
        var check = new IcsFeedCheck(reader);
        var add = new AddCalendarPage(feeds, check, () => new MicrosoftCalendarPicker(signIn, graph, feeds));
        var manage = new ManageCalendarsPage(feeds, check, add);
        Func<AccountSignIn, ICommand> signInAgain = account => new SignInAgainCommand(signIn, account, () => refresher.Refresh(force: true));

        var calendar = new CommandItem(new CalendarPage(feeds, source, refresher, manage, time, signInAgain))
        {
            Title = "Calendar",
            Subtitle = "Your events, day by day",
            MoreCommands = [new CommandContextItem(manage)],
        };
        var joinNext = new JoinNextMeetingItem(
            new JoinNextMeetingCommand(source, time),
            new CalendarPage(feeds, source, refresher, manage, time, signInAgain, JoinNextMeetingItem.CommandId));
        _ticker = new NextMeetingTicker(calendar, joinNext, feeds, source, refresher, time);
        _commands = [calendar, joinNext];
    }

    public override ICommandItem[] TopLevelCommands() => _commands;

    public override void Dispose()
    {
        _ticker.Dispose();
        base.Dispose();
    }
}
