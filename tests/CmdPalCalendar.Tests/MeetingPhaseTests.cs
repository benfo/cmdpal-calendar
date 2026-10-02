using CmdPalCalendar.Events;

namespace CmdPalCalendar.Tests;

public sealed class MeetingPhaseTests
{
    private static readonly DateTimeOffset Day = new(2026, 10, 5, 0, 0, 0, TimeSpan.Zero);
    private static readonly CalendarEntry Standup =
        new("standup", "Standup", Day.AddHours(9), Day.AddHours(9).AddMinutes(15), false, null, null, null, [], null, "test");

    [Theory]
    [InlineData(8, 59, "Upcoming")]
    [InlineData(9, 0, "Happening")]
    [InlineData(9, 14, "Happening")]
    [InlineData(9, 15, "Ended")]
    public void Knows_whether_a_meeting_is_upcoming_happening_or_ended(int hour, int minute, string phase)
    {
        Assert.Equal(phase, MeetingPhases.At(Standup, At(hour, minute)).ToString());
    }

    [Fact]
    public void Nothing_changes_while_every_meeting_stays_in_its_phase()
    {
        Assert.False(MeetingPhases.ChangedBetween([Standup], At(8, 30), At(8, 59)));
    }

    [Theory]
    [InlineData(8, 59, 9, 0)]
    [InlineData(9, 14, 9, 15)]
    public void Changes_when_a_meeting_starts_or_ends(int fromHour, int fromMinute, int toHour, int toMinute)
    {
        Assert.True(MeetingPhases.ChangedBetween([Standup], At(fromHour, fromMinute), At(toHour, toMinute)));
    }

    [Fact]
    public void Changes_at_midnight()
    {
        Assert.True(MeetingPhases.ChangedBetween([], At(23, 59), Day.AddDays(1)));
    }

    private static DateTimeOffset At(int hour, int minute) => Day.AddHours(hour).AddMinutes(minute);
}
