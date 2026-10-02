using System;
using CmdPalCalendar.Events;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace CmdPalCalendar.Pages;

internal sealed partial class JoinNextMeetingCommand : InvokableCommand
{
    private readonly ICalendarSource _source;
    private readonly TimeProvider _time;

    public JoinNextMeetingCommand(ICalendarSource source, TimeProvider time)
    {
        _source = source;
        _time = time;
        Id = "CmdPalCalendar.JoinNext";
        Name = "Join";
        Icon = new IconInfo(Glyphs.Video);
    }

    public override ICommandResult Invoke()
    {
        var now = _time.GetLocalNow();
        var today = DateOnly.FromDateTime(now.DateTime);

        return UpcomingMeeting.Find(_source.GetEntries(today, today.AddDays(1)), now) switch
        {
            null => CommandResult.ShowToast("No more meetings today"),
            { } meeting when MeetingLink.Parse(meeting.Link) is { } link => new JoinMeetingCommand(link).Invoke(),
            { } meeting => CommandResult.ShowToast($"{meeting.Title} has no meeting link"),
        };
    }
}
