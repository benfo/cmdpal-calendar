using System;
using System.Linq;
using System.Threading;
using CmdPalCalendar.Feeds;
using CmdPalCalendar.Graph;
using CmdPalCalendar.Ics;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace CmdPalCalendar.Pages;

internal sealed partial class ManageCalendarsPage : ListPage
{
    private readonly CalendarFeedStore _store;
    private readonly IcsFeedCheck _check;
    private readonly AddCalendarPage _add;
    private readonly MicrosoftAccounts _accounts;
    private readonly Func<MicrosoftAccount?, MicrosoftCalendarChoice> _newChoice;

    public ManageCalendarsPage(
        CalendarFeedStore store,
        IcsFeedCheck check,
        AddCalendarPage add,
        MicrosoftAccounts accounts,
        Func<MicrosoftAccount?, MicrosoftCalendarChoice> newChoice)
    {
        _store = store;
        _check = check;
        _add = add;
        _accounts = accounts;
        _newChoice = newChoice;
        _store.Changed += (_, _) => RaiseItemsChanged();

        Id = "CmdPalCalendar.ManageCalendars";
        Name = "Manage calendars";
        Title = "Calendars";
        Icon = new IconInfo(Glyphs.Settings);
        PlaceholderText = "Filter calendars";
    }

    public override IListItem[] GetItems() =>
    [
        new ListItem(_add)
        {
            Title = "Add a calendar",
            Subtitle = "Choose where your calendar comes from",
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
                new CommandContextItem(ChangeSourcePage(feed)),
                new CommandContextItem(new AnonymousCommand(() => _store.Update(feed with { Enabled = !feed.Enabled }))
                {
                    Name = feed.Enabled ? "Turn off" : "Turn on",
                    Result = CommandResult.KeepOpen(),
                }),
                new CommandContextItem(RemoveCommand(feed)) { IsCritical = true },
                .. feed.Source is MicrosoftSource source ? [new CommandContextItem(SignOutCommand(source)) { IsCritical = true }] : Array.Empty<IContextItem>(),
            ],
        };

    private ContentPage ChangeSourcePage(CalendarFeed feed) => feed.Source switch
    {
        MicrosoftSource source => new MicrosoftCalendarsPage(() => _newChoice(new MicrosoftAccount(source.AccountId, source.Account)))
        {
            Name = $"Choose calendars for {source.Account}…",
            Icon = new IconInfo(Glyphs.Calendar),
        },
        _ => new IcsAddressFormPage(_store, _check, feed),
    };

    private AnonymousCommand RemoveCommand(CalendarFeed feed) =>
        Confirmed(
            "Remove",
            Glyphs.Delete,
            $"Remove {feed.Name}?",
            _accounts.IsLastOfItsAccount(feed)
                ? $"Its events will no longer show, and you'll be signed out of {feed.Source.Label}. You can add it again later."
                : "Its events will no longer show. You can add it again later.",
            () => _ = _accounts.RemoveCalendarAsync(feed, CancellationToken.None));

    private AnonymousCommand SignOutCommand(MicrosoftSource source)
    {
        var count = _accounts.CalendarCount(source.AccountId);
        return Confirmed(
            $"Sign out of {source.Account}",
            Glyphs.Contact,
            $"Sign out of {source.Account}?",
            count == 1
                ? "Its calendar will be removed. You can add it again later."
                : $"Its {count} calendars will be removed. You can add them again later.",
            () => _ = _accounts.SignOutAsync(source.AccountId, CancellationToken.None));
    }

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
