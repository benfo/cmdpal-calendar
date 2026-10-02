using System;
using System.Collections.Generic;
using CmdPalCalendar.Events;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace CmdPalCalendar.Pages;

internal sealed partial class JoinNextMeetingItem : CommandItem
{
    public const string CommandId = "CmdPalCalendar.JoinNext";

    private readonly ICommand _join;
    private readonly ICommand _calendar;

    public JoinNextMeetingItem(ICommand join, ICommand calendar)
        : base(join)
    {
        _join = join;
        _calendar = calendar;
        Title = "Join next meeting";
    }

    public void Show(CalendarEntry? joinable, DateTimeOffset now)
    {
        Command = joinable is null ? _calendar : _join;
        Subtitle = Describe(joinable, now);
    }

    private static string Describe(CalendarEntry? meeting, DateTimeOffset now)
    {
        if (meeting is null)
        {
            return "Nothing to join today";
        }

        var service = MeetingServiceText.Name(MeetingLink.Parse(meeting.Link)!.Service);
        IEnumerable<string> parts = meeting.Start <= now ? [$"Now: {meeting.Title}", service!]
            : UpcomingMeeting.IsJoinableNow(meeting, now) ? [$"{meeting.Title} in {TimeText.Duration(meeting.Start - now)}", service!]
            : [$"Next to join: {meeting.Title} at {TimeText.Clock(meeting.Start)}"];

        return string.Join(" · ", parts);
    }
}
