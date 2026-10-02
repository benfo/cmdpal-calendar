using System.Linq;
using CmdPalCalendar.Feeds;
using CmdPalCalendar.Ics;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace CmdPalCalendar.Pages;

internal sealed partial class ManageCalendarsPage : ListPage
{
    private readonly CalendarFeedStore _store;
    private readonly IcsFeedCheck _check;

    public ManageCalendarsPage(CalendarFeedStore store, IcsFeedCheck check)
    {
        _store = store;
        _check = check;
        _store.Changed += (_, _) => RaiseItemsChanged();

        Id = "CmdPalCalendar.ManageCalendars";
        Name = "Manage calendars";
        Title = "Calendars";
        Icon = new IconInfo(Glyphs.Settings);
        PlaceholderText = "Filter calendars";
    }

    public override IListItem[] GetItems() =>
    [
        new ListItem(new CalendarFeedFormPage(_store, _check, feed: null))
        {
            Title = "Add a calendar",
            Subtitle = "A published calendar address (ICS) or an .ics file",
            Icon = new IconInfo(Glyphs.Add),
        },
        .. _store.Feeds.Select(FeedItem),
    ];

    private ListItem FeedItem(CalendarFeed feed) =>
        new(new CalendarFeedFormPage(_store, _check, feed))
        {
            Title = feed.Name,
            Subtitle = feed.Enabled ? feed.LocationLabel : $"{feed.LocationLabel} · Off",
            Icon = new IconInfo(Glyphs.Calendar),
            Tags = [CalendarColors.Tag(feed.Color.ToString(), feed.Color)],
            MoreCommands =
            [
                new CommandContextItem(new AnonymousCommand(() => _store.Update(feed with { Enabled = !feed.Enabled }))
                {
                    Name = feed.Enabled ? "Turn off" : "Turn on",
                    Result = CommandResult.KeepOpen(),
                }),
                new CommandContextItem(RemoveCommand(feed)) { IsCritical = true },
            ],
        };

    private AnonymousCommand RemoveCommand(CalendarFeed feed) =>
        new(action: null)
        {
            Name = "Remove",
            Icon = new IconInfo(Glyphs.Delete),
            Result = CommandResult.Confirm(new ConfirmationArgs
            {
                Title = $"Remove {feed.Name}?",
                Description = "Its events will no longer show. You can add it again later.",
                PrimaryCommand = new AnonymousCommand(() => _store.Remove(feed.Id)) { Name = "Remove", Result = CommandResult.KeepOpen() },
                IsPrimaryCommandCritical = true,
            }),
        };
}
