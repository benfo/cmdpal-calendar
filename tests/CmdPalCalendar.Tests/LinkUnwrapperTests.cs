using CmdPalCalendar.Events;

namespace CmdPalCalendar.Tests;

public sealed class LinkUnwrapperTests
{
    private const string Zoom = "https://acme.zoom.us/j/1234567890?pwd=abc123";

    [Theory]
    [InlineData("https://eur02.safelinks.protection.outlook.com/?url=https%3A%2F%2Facme.zoom.us%2Fj%2F1234567890%3Fpwd%3Dabc123&data=05%7C02&sdata=xyz&reserved=0")]
    [InlineData("https://eur02.safelinks.protection.outlook.com/?url=https%3A%2F%2Facme.zoom.us%2Fj%2F1234567890%3Fpwd%3Dabc123&amp;data=05&amp;reserved=0")]
    [InlineData("https://www.google.com/url?q=https%3A%2F%2Facme.zoom.us%2Fj%2F1234567890%3Fpwd%3Dabc123&sa=D&source=calendar")]
    [InlineData("https://www.google.co.za/url?q=https://acme.zoom.us/j/1234567890?pwd%3Dabc123&amp;sa=D")]
    [InlineData("https://urldefense.com/v3/__https://acme.zoom.us/j/1234567890?pwd=abc123__;!!abc!def$")]
    public void Unwraps_redirects(string wrapped)
    {
        Assert.Equal(Zoom, LinkUnwrapper.Unwrap(wrapped));
    }

    [Fact]
    public void Unwraps_nested_redirects()
    {
        var google = "https://www.google.com/url?q=" + Uri.EscapeDataString(Zoom);
        var safeLinks = "https://nam12.safelinks.protection.outlook.com/?url=" + Uri.EscapeDataString(google) + "&data=1";

        Assert.Equal(Zoom, LinkUnwrapper.Unwrap(safeLinks));
    }

    [Theory]
    [InlineData(Zoom)]
    [InlineData("https://meet.google.com/abc-defg-hij")]
    [InlineData("https://teams.microsoft.com/l/meetup-join/19%3ameeting_x%40thread.v2/0?context=%7b%7d")]
    public void Leaves_plain_links_alone(string url)
    {
        Assert.Equal(url, LinkUnwrapper.Unwrap(url));
    }

    [Fact]
    public void Finder_prefers_the_unwrapped_meeting_link()
    {
        var description = "Agenda: https://example.com/agenda\nJoin: https://www.google.com/url?q=" + Uri.EscapeDataString(Zoom) + "&sa=D";

        Assert.Equal(Zoom, MeetingLinkFinder.Find(description));
    }
}
