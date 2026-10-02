using CmdPalCalendar.Events;

namespace CmdPalCalendar.Tests;

public sealed class UpcomingMeetingTests
{
    private static readonly DateTimeOffset Day = new(2026, 10, 5, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Picks_the_meeting_in_progress()
    {
        var review = Meeting("Review", 10, 0, 11, 0);

        Assert.Same(review, UpcomingMeeting.Find([review, Meeting("Lunch", 12, 30, 13, 30)], At(10, 20)));
    }

    [Fact]
    public void Picks_the_next_meeting_once_it_is_about_to_start()
    {
        var review = Meeting("Review", 10, 0, 11, 0);
        var standup = Meeting("Standup", 10, 58, 11, 15);

        Assert.Same(review, UpcomingMeeting.Find([review, standup], At(10, 50)));
        Assert.Same(standup, UpcomingMeeting.Find([review, standup], At(10, 54)));
    }

    [Fact]
    public void Picks_the_latest_started_of_overlapping_meetings()
    {
        var workshop = Meeting("Workshop", 9, 0, 12, 0);
        var sync = Meeting("Sync", 10, 0, 10, 30);

        Assert.Same(sync, UpcomingMeeting.Find([workshop, sync], At(10, 10)));
    }

    [Fact]
    public void Picks_the_next_meeting_when_nothing_is_in_progress()
    {
        var lunch = Meeting("Lunch", 12, 30, 13, 30);

        Assert.Same(lunch, UpcomingMeeting.Find([Meeting("Standup", 9, 0, 9, 15), lunch], At(11, 0)));
    }

    [Fact]
    public void Ignores_all_day_and_finished_events()
    {
        CalendarEntry[] entries = [Meeting("Standup", 9, 0, 9, 15), Meeting("Holiday", 0, 0, 0, 0, allDay: true)];

        Assert.Null(UpcomingMeeting.Find(entries, At(11, 0)));
    }

    [Fact]
    public void Joinable_skips_meetings_without_a_meeting_link()
    {
        var inPerson = Meeting("1:1", 10, 0, 10, 30);
        var agenda = Meeting("Planning", 10, 0, 11, 0, link: "https://example.com/agenda");
        var call = Meeting("Vendor call", 10, 30, 11, 0, link: "https://acme.zoom.us/j/1234567890");

        Assert.Same(call, UpcomingMeeting.FindJoinable([inPerson, agenda, call], At(10, 5)));
    }

    [Theory]
    [InlineData(10, 20, false)]
    [InlineData(10, 44, false)]
    [InlineData(10, 45, true)]
    [InlineData(11, 10, true)]
    public void Joinable_now_within_fifteen_minutes_or_once_started(int hour, int minute, bool expected)
    {
        var call = Meeting("Call", 11, 0, 11, 30);

        Assert.Equal(expected, UpcomingMeeting.IsJoinableNow(call, At(hour, minute)));
    }

    private static DateTimeOffset At(int hour, int minute) => Day.AddHours(hour).AddMinutes(minute);

    private static CalendarEntry Meeting(string title, int startHour, int startMinute, int endHour, int endMinute, bool allDay = false, string? link = null) =>
        new(title, title, At(startHour, startMinute), allDay ? Day.AddDays(1) : At(endHour, endMinute), allDay, null, null, null, [], link, "test");
}
