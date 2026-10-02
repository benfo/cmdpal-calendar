using CmdPalCalendar.Events;

namespace CmdPalCalendar.Tests;

public sealed class MeetingLinkTests
{
    [Theory]
    [InlineData(
        "https://teams.microsoft.com/l/meetup-join/19%3ameeting_abc%40thread.v2/0?context=%7b%22Tid%22%3a%22x%22%7d",
        "msteams://teams.microsoft.com/l/meetup-join/19%3ameeting_abc%40thread.v2/0?context=%7b%22Tid%22%3a%22x%22%7d")]
    [InlineData("https://teams.microsoft.com/meet/2345678901234?p=AbCdEf123", "msteams://teams.microsoft.com/meet/2345678901234?p=AbCdEf123")]
    [InlineData("https://teams.live.com/meet/9876543210?p=xyz", "msteams://teams.live.com/meet/9876543210?p=xyz")]
    [InlineData("https://gov.teams.microsoft.us/l/meetup-join/19%3ameeting_g%40thread.v2/0", "msteams://gov.teams.microsoft.us/l/meetup-join/19%3ameeting_g%40thread.v2/0")]
    [InlineData("https://dod.teams.microsoft.us/meet/123?p=a", "msteams://dod.teams.microsoft.us/meet/123?p=a")]
    public void Teams_links_open_in_the_app(string url, string app)
    {
        var link = MeetingLink.Parse(url)!;

        Assert.Equal(MeetingService.Teams, link.Service);
        Assert.Equal(app, link.App!.OriginalString);
        Assert.Equal(url, link.Web.OriginalString);
    }

    [Theory]
    [InlineData("https://teams.microsoft.com/meetingOptions/?organizerId=x")]
    [InlineData("https://example.com/agenda")]
    public void Other_links_open_in_the_browser(string url)
    {
        var link = MeetingLink.Parse(url)!;

        Assert.Equal(MeetingService.Other, link.Service);
        Assert.Null(link.App);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("not a url")]
    [InlineData("mailto:someone@example.com")]
    public void Ignores_anything_that_is_not_a_web_link(string? url)
    {
        Assert.Null(MeetingLink.Parse(url));
    }
}
