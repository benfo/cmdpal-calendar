using CmdPalCalendar.Events;

namespace CmdPalCalendar.Tests;

public sealed class EventNotesTests
{
    [Fact]
    public void Prefers_the_html_version()
    {
        Assert.Equal("**Agenda** for today", EventNotes.From("Agenda for today", "<html><body><b>Agenda</b> for today</body></html>"));
    }

    [Fact]
    public void Converts_a_description_that_contains_html()
    {
        Assert.Equal("Bring your **laptop**", EventNotes.From("Bring your <b>laptop</b>", html: null));
    }

    [Fact]
    public void Escapes_a_plain_text_description_and_keeps_its_lines()
    {
        Assert.Equal("Step \\#1  \nStep \\#2\n\nNotes \\*important\\*", EventNotes.From("Step #1\nStep #2\n\n\nNotes *important*", html: null));
    }

    [Fact]
    public void Cuts_the_teams_invite_block()
    {
        const string description = "Weekly sync on the roadmap.\n\n________________________________________________________________________________\nMicrosoft Teams meeting\nJoin on your computer, mobile app or room device\nClick here to join the meeting\nMeeting ID: 345 678 901 234\nPasscode: Xy7Zq2";

        Assert.Equal("Weekly sync on the roadmap.", EventNotes.From(description, html: null));
    }

    [Fact]
    public void Cuts_the_teams_invite_block_in_html()
    {
        const string html = "<p>Roadmap review</p><div>________________________________________________________________________________</div><div><b>Microsoft Teams meeting</b></div><div>Join: <a href=\"https://teams.microsoft.com/l/meetup-join/x\">Click here</a></div>";

        Assert.Equal("Roadmap review", EventNotes.From(description: null, html));
    }

    [Fact]
    public void Cuts_the_zoom_invite_block()
    {
        const string description = "Quarterly planning\nBring numbers\n\nJoin Zoom Meeting\nhttps://acme.zoom.us/j/1234567890?pwd=abc\n\nMeeting ID: 123 456 7890";

        Assert.Equal("Quarterly planning  \nBring numbers", EventNotes.From(description, html: null));
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("   ", null)]
    [InlineData("Join Zoom Meeting\nhttps://acme.zoom.us/j/1234567890", null)]
    public void Returns_null_when_nothing_is_left(string? description, string? html)
    {
        Assert.Null(EventNotes.From(description, html));
    }
}
