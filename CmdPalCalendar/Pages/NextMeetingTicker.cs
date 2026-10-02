using System;
using System.Threading;
using CmdPalCalendar.Events;
using CmdPalCalendar.Feeds;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace CmdPalCalendar.Pages;

internal sealed partial class NextMeetingTicker : IDisposable
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan CountdownWindow = TimeSpan.FromHours(1);

    private readonly CommandItem _calendar;
    private readonly JoinNextMeetingItem _joinNext;
    private readonly CalendarFeedStore _feeds;
    private readonly ICalendarSource _source;
    private readonly CalendarRefresher _refresher;
    private readonly TimeProvider _time;
    private readonly ITimer _timer;

    public NextMeetingTicker(
        CommandItem calendar,
        JoinNextMeetingItem joinNext,
        CalendarFeedStore feeds,
        ICalendarSource source,
        CalendarRefresher refresher,
        TimeProvider time)
    {
        _calendar = calendar;
        _joinNext = joinNext;
        _feeds = feeds;
        _source = source;
        _refresher = refresher;
        _time = time;

        _refresher.LoadingChanged += (_, _) => Update();
        _feeds.Changed += (_, _) => Update();
        _timer = time.CreateTimer(_ => Tick(), null, TimeSpan.Zero, Interval);
    }

    public void Dispose() => _timer.Dispose();

    private void Tick()
    {
        _refresher.Refresh();
        Update();
    }

    private void Update()
    {
        if (_feeds.EnabledFeeds.Count == 0)
        {
            _calendar.Subtitle = _joinNext.Subtitle = _feeds.Feeds.Count == 0 ? "Add a calendar to get started" : "All calendars are turned off";
            return;
        }

        if (_refresher.IsLoading)
        {
            return;
        }

        var now = _time.GetLocalNow();
        var today = DateOnly.FromDateTime(now.DateTime);
        var entries = _source.GetEntries(today, today.AddDays(1));

        _calendar.Subtitle = DescribeNext(UpcomingMeeting.Find(entries, now), now);
        _joinNext.Show(UpcomingMeeting.FindJoinable(entries, now), now);
    }

    private static string DescribeNext(CalendarEntry? meeting, DateTimeOffset now) => meeting switch
    {
        null => "No more meetings today",
        _ when meeting.Start <= now => $"Now: {meeting.Title} · until {TimeText.Clock(meeting.End)}",
        _ when meeting.Start - now < CountdownWindow => $"Next: {meeting.Title} in {TimeText.Duration(meeting.Start - now)}",
        _ => $"Next: {meeting.Title} at {TimeText.Clock(meeting.Start)}",
    };
}
