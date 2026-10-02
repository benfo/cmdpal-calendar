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
    [InlineData("https://acme.zoom.us/j/1234567890?pwd=abc123", "zoommtg://acme.zoom.us/join?action=join&confno=1234567890&pwd=abc123")]
    [InlineData("https://zoom.us/j/98765432101", "zoommtg://zoom.us/join?action=join&confno=98765432101")]
    [InlineData("https://us02web.zoom.us/j/1234567890?pwd=abc&uname=Ben&tk=secret", "zoommtg://us02web.zoom.us/join?action=join&confno=1234567890&pwd=abc&uname=Ben")]
    [InlineData("https://acme.zoomgov.com/j/1612345678", "zoommtg://acme.zoomgov.com/join?action=join&confno=1612345678")]
    [InlineData("https://acme.zoom.com/wc/join/1234567890?pwd=x", "zoommtg://acme.zoom.com/join?action=join&confno=1234567890&pwd=x")]
    public void Zoom_meeting_links_open_in_the_app(string url, string app)
    {
        var link = MeetingLink.Parse(url)!;

        Assert.Equal(MeetingService.Zoom, link.Service);
        Assert.Equal(app, link.App!.OriginalString);
    }

    [Theory]
    [InlineData("https://acme.zoom.us/my/ben.fourie")]
    [InlineData("https://acme.zoom.us/w/1234567890?tk=abc")]
    [InlineData("https://acme.zoom.us/s/1234567890")]
    [InlineData("https://acme.zoom.us/meeting/register/tJ0abc")]
    public void Zoom_links_the_app_cannot_join_open_in_the_browser(string url)
    {
        var link = MeetingLink.Parse(url)!;

        Assert.Equal(MeetingService.Zoom, link.Service);
        Assert.Null(link.App);
    }

    [Theory]
    [InlineData("https://meet.google.com/abc-defg-hij", "Meet")]
    [InlineData("https://acme.webex.com/meet/ben", "Webex")]
    [InlineData("https://acme.webex.com/acme/j.php?MTID=m123", "Webex")]
    public void Browser_only_services_are_recognised(string url, string service)
    {
        var link = MeetingLink.Parse(url)!;

        Assert.Equal(service, link.Service.ToString());
        Assert.Null(link.App);
    }

    [Theory]
    [InlineData("https://teams.microsoft.com/meetingOptions/?organizerId=x")]
    [InlineData("https://zoom.example.com/j/1234567890")]
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
