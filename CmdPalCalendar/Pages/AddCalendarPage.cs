using CmdPalCalendar.Feeds;
using CmdPalCalendar.Ics;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace CmdPalCalendar.Pages;

internal sealed partial class AddCalendarPage : ListPage
{
    private readonly CalendarFeedStore _store;
    private readonly IcsFeedCheck _check;

    public AddCalendarPage(CalendarFeedStore store, IcsFeedCheck check)
    {
        _store = store;
        _check = check;

        Id = "CmdPalCalendar.AddCalendar";
        Name = "Add";
        Title = "Add a calendar";
        Icon = new IconInfo(Glyphs.Add);
        PlaceholderText = "Where is your calendar?";
    }

    public override IListItem[] GetItems() =>
    [
        new ListItem(new CalendarFeedFormPage(_store, _check, feed: null))
        {
            Title = "Calendar address or file",
            Subtitle = "A published calendar (ICS) or an .ics file",
            Icon = new IconInfo(Glyphs.Link),
        },
    ];
}
