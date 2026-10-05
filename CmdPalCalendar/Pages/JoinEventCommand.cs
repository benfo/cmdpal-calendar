using System;
using CmdPalCalendar.Events;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace CmdPalCalendar.Pages;

internal sealed partial class JoinEventCommand : InvokableCommand
{
    private readonly CalendarEntry _meeting;
    private readonly JoinMeetingCommand _join;
    private readonly TimeProvider _time;

    public JoinEventCommand(CalendarEntry meeting, MeetingLink link, TimeProvider time)
    {
        _meeting = meeting;
        _join = new JoinMeetingCommand(link);
        _time = time;
        Name = _join.Name;
        Icon = _join.Icon;
    }

    public override ICommandResult Invoke()
    {
        var now = _time.GetLocalNow();
        return UpcomingMeeting.IsJoinableNow(_meeting, now)
            ? _join.Invoke()
            : CommandResult.Confirm(new ConfirmationArgs
            {
                Title = $"Join {_meeting.Title}?",
                Description = now >= _meeting.End
                    ? $"{_meeting.Title} ended {When(_meeting.End, now)}. Join anyway?"
                    : $"{_meeting.Title} starts {When(_meeting.Start, now)} (in {TimeText.Duration(_meeting.Start - now)}). Join now?",
                PrimaryCommand = _join,
            });
    }

    private static string When(DateTimeOffset time, DateTimeOffset now)
    {
        var day = DateOnly.FromDateTime(time.DateTime);
        var today = DateOnly.FromDateTime(now.DateTime);
        var date = (day.DayNumber - today.DayNumber) switch
        {
            0 => string.Empty,
            1 => "tomorrow ",
            -1 => "yesterday ",
            _ => $"on {DateText.Short(day)} ",
        };
        return $"{date}at {TimeText.Clock(time)}";
    }
}
