using CmdPalCalendar.Events;

namespace CmdPalCalendar.Tests;

public sealed class MeetingCredentialsTests
{
    [Fact]
    public void Finds_zoom_meeting_id_and_passcode()
    {
        var credentials = MeetingCredentials.Find("Join Zoom Meeting\nhttps://acme.zoom.us/j/1234567890?pwd=x\n\nMeeting ID: 123 456 7890\nPasscode: abc123\n");

        Assert.Equal(new MeetingCredentials("123 456 7890", "abc123"), credentials);
        Assert.Equal("Meeting ID: 123 456 7890 · Passcode: abc123", credentials!.ToString());
    }

    [Fact]
    public void Finds_teams_meeting_id_and_passcode()
    {
        var credentials = MeetingCredentials.Find("Microsoft Teams meeting\nMeeting ID: 345 678 901 234 5\nPasscode: Xy7Zq2\nNeed help?");

        Assert.Equal(new MeetingCredentials("345 678 901 234 5", "Xy7Zq2"), credentials);
    }

    [Fact]
    public void Finds_a_passcode_on_its_own()
    {
        Assert.Equal(new MeetingCredentials(null, "9f8e7d"), MeetingCredentials.Find("Password: 9f8e7d"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Lunch with the team")]
    public void Returns_null_without_credentials(string? text)
    {
        Assert.Null(MeetingCredentials.Find(text));
    }
}
