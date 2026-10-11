using System;
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
        new ListItem(new IcsAddressFormPage(_store, _check, feed: null))
        {
            Title = "Add a calendar",
            Subtitle = "A published calendar (ICS) or an .ics file",
            Icon = new IconInfo(Glyphs.Add),
        },
        .. _store.Feeds.Select(FeedItem),
    ];

    private ListItem FeedItem(CalendarFeed feed) =>
        new(new CalendarDetailsFormPage(_store, feed))
        {
            Title = feed.Name,
            Subtitle = feed.Enabled ? feed.Source.Label : $"{feed.Source.Label} · Off",
            Icon = new IconInfo(Glyphs.Calendar),
            Tags = [CalendarColors.Tag(feed.Color.ToString(), feed.Color)],
            MoreCommands =
            [
                new CommandContextItem(new IcsAddressFormPage(_store, _check, feed)),
                new CommandContextItem(new AnonymousCommand(() => _store.Update(feed with { Enabled = !feed.Enabled }))
                {
                    Name = feed.Enabled ? "Turn off" : "Turn on",
                    Result = CommandResult.KeepOpen(),
                }),
                new CommandContextItem(RemoveCommand(feed)) { IsCritical = true },
            ],
        };

    private AnonymousCommand RemoveCommand(CalendarFeed feed) =>
        Confirmed(
            "Remove",
            Glyphs.Delete,
            $"Remove {feed.Name}?",
            "Its events will no longer show. You can add it again later.",
            () => _store.Remove(feed.Id));

    private static AnonymousCommand Confirmed(string name, string glyph, string title, string description, Action action) =>
        new(action: null)
        {
            Name = name,
            Icon = new IconInfo(glyph),
            Result = CommandResult.Confirm(new ConfirmationArgs
            {
                Title = title,
                Description = description,
                PrimaryCommand = new AnonymousCommand(action) { Name = name, Result = CommandResult.KeepOpen() },
                IsPrimaryCommandCritical = true,
            }),
        };
}
