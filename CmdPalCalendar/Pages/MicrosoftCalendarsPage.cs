using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CmdPalCalendar.Graph;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace CmdPalCalendar.Pages;

internal sealed partial class MicrosoftCalendarsPage : ListPage
{
    private readonly MicrosoftCalendarPicker _picker;
    private int _opened;

    public MicrosoftCalendarsPage(MicrosoftCalendarPicker picker)
    {
        _picker = picker;
        _picker.Changed += (_, _) => RaiseItemsChanged();

        Id = "CmdPalCalendar.MicrosoftCalendars";
        Name = "Choose";
        Title = "Choose calendars";
        Icon = new IconInfo(Glyphs.Calendar);
        PlaceholderText = "Filter calendars";
    }

    public override IListItem[] GetItems()
    {
        if (Interlocked.Exchange(ref _opened, 1) == 0)
        {
            Start(_picker.OpenAsync);
        }

        IsLoading = _picker.IsSigningIn || _picker.Accounts.Any(a => a.IsLoading);
        return [SignInItem(), .. _picker.Accounts.SelectMany(AccountItems)];
    }

    private IListItem[] AccountItems(PickerAccount account) => account switch
    {
        { Calendars: { } calendars } => calendars.Select(c => CalendarItem(account.Account, c)).ToArray(),
        { Error: { } error } => [new ListItem(SignInCommand()) { Title = error, Subtitle = account.Account.Username, Icon = new IconInfo(Glyphs.Error), Section = account.Account.Username }],
        _ => [new ListItem(new NoOpCommand()) { Title = "Loading calendars…", Section = account.Account.Username }],
    };

    private ListItem CalendarItem(MicrosoftAccount account, GraphCalendar calendar)
    {
        var added = _picker.IsAdded(account, calendar);
        return new ListItem(added ? new NoOpCommand() : AddCommand(account, calendar))
        {
            Title = calendar.Name,
            Subtitle = calendar.IsDefault ? "Your default calendar" : calendar.SharedBy is { } owner ? $"Shared by {owner}" : string.Empty,
            Icon = new IconInfo(added ? Glyphs.Accept : Glyphs.Calendar),
            Tags = added ? [new Tag("Added")] : [],
            Section = account.Username,
        };
    }

    private AnonymousCommand AddCommand(MicrosoftAccount account, GraphCalendar calendar) =>
        new(() =>
        {
            _picker.Add(account, calendar);
            RaiseItemsChanged();
        })
        {
            Name = "Add",
            Icon = new IconInfo(Glyphs.Add),
            Result = CommandResult.ShowToast(new ToastArgs { Message = $"Added {calendar.Name}", Result = CommandResult.KeepOpen() }),
        };

    private ListItem SignInItem() =>
        new(SignInCommand())
        {
            Title = _picker.Accounts.Count == 0 ? "Sign in" : "Add another Microsoft account",
            Subtitle = _picker.IsSigningIn ? "Signing in…" : _picker.SignInProblem ?? "Work, school or personal Microsoft account",
            Icon = new IconInfo(Glyphs.Contact),
        };

    private AnonymousCommand SignInCommand() =>
        new(() => Start(_picker.SignInAsync))
        {
            Name = "Sign in",
            Icon = new IconInfo(Glyphs.Contact),
            Result = CommandResult.KeepOpen(),
        };

    private static void Start(Func<CancellationToken, Task> action) => _ = Task.Run(() => action(CancellationToken.None));
}
