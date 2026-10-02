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
        Id = JoinNextMeetingItem.CommandId;
        Name = "Join";
        Icon = new IconInfo(Glyphs.Video);
    }

    public override ICommandResult Invoke()
    {
        var now = _time.GetLocalNow();
        var today = DateOnly.FromDateTime(now.DateTime);

        if (UpcomingMeeting.FindJoinable(_source.GetEntries(today, today.AddDays(1)), now) is not { } meeting ||
            MeetingLink.Parse(meeting.Link) is not { } link)
        {
            return CommandResult.ShowToast("Nothing to join today");
        }

        var join = new JoinMeetingCommand(link);
        return UpcomingMeeting.IsJoinableNow(meeting, now)
            ? join.Invoke()
            : CommandResult.Confirm(new ConfirmationArgs
            {
                Title = $"Join {meeting.Title}?",
                Description = $"{meeting.Title} starts at {TimeText.Clock(meeting.Start)} (in {TimeText.Duration(meeting.Start - now)}). Join now?",
                PrimaryCommand = join,
            });
    }
}
