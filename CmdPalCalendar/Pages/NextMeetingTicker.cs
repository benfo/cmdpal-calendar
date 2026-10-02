using System;
using System.Threading;
using CmdPalCalendar.Events;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace CmdPalCalendar.Pages;

internal sealed partial class NextMeetingTicker : IDisposable
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan CountdownWindow = TimeSpan.FromHours(1);

    private readonly CommandItem _item;
    private readonly CalendarSettings _settings;
    private readonly ICalendarSource _source;
    private readonly CalendarRefresher _refresher;
    private readonly TimeProvider _time;
    private readonly ITimer _timer;

    public NextMeetingTicker(CommandItem item, CalendarSettings settings, ICalendarSource source, CalendarRefresher refresher, TimeProvider time)
    {
        _item = item;
        _settings = settings;
        _source = source;
        _refresher = refresher;
        _time = time;

        _refresher.LoadingChanged += (_, _) => Update();
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
        if (_settings.IcsFeeds.Count == 0)
        {
            _item.Subtitle = "Add an ICS feed in settings";
            return;
        }

        if (_refresher.IsLoading)
        {
            return;
        }

        var now = _time.GetLocalNow();
        var today = DateOnly.FromDateTime(now.DateTime);
        _item.Subtitle = Describe(UpcomingMeeting.Find(_source.GetEntries(today, today.AddDays(1)), now), now);
    }

    private static string Describe(CalendarEntry? meeting, DateTimeOffset now) => meeting switch
    {
        null => "No more meetings today",
        _ when meeting.Start <= now => $"Now: {meeting.Title} · until {TimeText.Clock(meeting.End)}",
        _ when meeting.Start - now < CountdownWindow => $"Next: {meeting.Title} in {TimeText.Duration(meeting.Start - now)}",
        _ => $"Next: {meeting.Title} at {TimeText.Clock(meeting.Start)}",
    };
}
