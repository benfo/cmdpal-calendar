using System.Net;
using System.Text;
using CmdPalCalendar.Events;
using CmdPalCalendar.Feeds;
using CmdPalCalendar.Graph;

namespace CmdPalCalendar.Tests;

public sealed class GraphClientTests
{
    private static readonly MicrosoftAccount Ben = new("account-1", "ben@example.com");

    [Fact]
    public async Task Lists_calendars_default_first_and_shared_last()
    {
        var handler = new FakeGraph(
            """
            { "value": [
                { "id": "c2", "name": "Team", "color": "lightTeal", "isDefaultCalendar": false, "owner": { "name": "Jane Doe", "address": "jane@example.com" } },
                { "id": "c3", "name": "Birthdays", "color": "auto", "isDefaultCalendar": false, "owner": { "name": "Ben", "address": "BEN@example.com" } },
                { "id": "c1", "name": "Calendar", "color": "lightBlue", "isDefaultCalendar": true, "owner": { "name": "Ben", "address": "ben@example.com" } }
            ] }
            """);

        var calendars = await Client(handler).GetCalendarsAsync(Ben, CancellationToken.None);

        Assert.Equal(
            [
                new GraphCalendar("c1", "Calendar", null, true, CalendarColor.Blue),
                new GraphCalendar("c3", "Birthdays", null, false, null),
                new GraphCalendar("c2", "Team", "Jane Doe", false, CalendarColor.Teal),
            ],
            calendars);
    }

    [Fact]
    public async Task Follows_next_pages()
    {
        var handler = new FakeGraph(
            """{ "value": [ { "id": "c1", "name": "One" } ], "@odata.nextLink": "https://graph.microsoft.com/v1.0/me/calendars?$skip=1" }""",
            """{ "value": [ { "id": "c2", "name": "Two" } ] }""");

        var calendars = await Client(handler).GetCalendarsAsync(Ben, CancellationToken.None);

        Assert.Equal(["One", "Two"], calendars.Select(c => c.Name));
    }

    [Fact]
    public async Task Sends_the_account_token()
    {
        var handler = new FakeGraph("""{ "value": [] }""");

        await Client(handler).GetCalendarsAsync(Ben, CancellationToken.None);

        Assert.Equal("Bearer token-for-account-1", Assert.Single(handler.Authorizations));
    }

    [Fact]
    public async Task Maps_events_with_their_join_link_notes_and_web_link()
    {
        var handler = new FakeGraph(
            """
            { "value": [ {
                "id": "e1", "subject": "Standup", "isAllDay": false, "isCancelled": false,
                "start": { "dateTime": "2026-10-05T07:00:00.0000000", "timeZone": "UTC" },
                "end": { "dateTime": "2026-10-05T07:15:00.0000000", "timeZone": "UTC" },
                "location": { "displayName": "Room 4" },
                "body": { "contentType": "html", "content": "<p><b>Agenda</b></p><p>Meeting ID: 123 456 789</p>" },
                "organizer": { "emailAddress": { "name": "Jane Doe", "address": "jane@example.com" } },
                "attendees": [ { "emailAddress": { "address": "sam@example.com" } } ],
                "onlineMeeting": { "joinUrl": "https://teams.microsoft.com/l/meetup-join/abc" },
                "webLink": "https://outlook.office365.com/owa/?itemid=e1&exvsurl=1&path=/calendar/item"
            } ] }
            """);

        var entry = Assert.Single(await Client(handler).GetEntriesAsync(Work, Monday, Monday.AddDays(1), CancellationToken.None));

        Assert.Equal("Standup", entry.Title);
        Assert.Equal(new DateTimeOffset(2026, 10, 5, 7, 0, 0, TimeSpan.Zero), entry.Start);
        Assert.Equal(new DateTimeOffset(2026, 10, 5, 7, 15, 0, TimeSpan.Zero), entry.End);
        Assert.Equal("Room 4", entry.Location);
        Assert.Equal("Jane Doe", entry.Organizer);
        Assert.Equal(["sam@example.com"], entry.Attendees);
        Assert.Equal("https://teams.microsoft.com/l/meetup-join/abc", entry.Link);
        Assert.Contains("**Agenda**", entry.Notes, StringComparison.Ordinal);
        Assert.Equal("Work", entry.Source);
        Assert.Equal(CalendarColor.Purple, entry.Color);
        Assert.Equal(CalendarSite.Outlook, entry.Site);
        Assert.Equal("outlook.office365.com", entry.WebLink?.Host);
    }

    [Fact]
    public async Task Keeps_all_day_events_on_their_date_and_skips_cancelled_ones()
    {
        var handler = new FakeGraph(
            """
            { "value": [
                { "id": "e1", "subject": "Holiday", "isAllDay": true,
                  "start": { "dateTime": "2026-10-05T00:00:00.0000000", "timeZone": "UTC" },
                  "end": { "dateTime": "2026-10-06T00:00:00.0000000", "timeZone": "UTC" } },
                { "id": "e2", "subject": "Cancelled", "isCancelled": true,
                  "start": { "dateTime": "2026-10-05T09:00:00.0000000", "timeZone": "UTC" },
                  "end": { "dateTime": "2026-10-05T10:00:00.0000000", "timeZone": "UTC" } }
            ] }
            """);

        var entry = Assert.Single(await Client(handler).GetEntriesAsync(Work, Monday, Monday.AddDays(1), CancellationToken.None));

        Assert.True(entry.IsAllDay);
        Assert.Equal(new DateTime(2026, 10, 5), entry.Start.DateTime);
        Assert.Equal(new DateTime(2026, 10, 6), entry.End.DateTime);
    }

    [Fact]
    public async Task Asks_for_the_calendar_view_in_utc_with_html_bodies()
    {
        var handler = new FakeGraph("""{ "value": [] }""");

        await Client(handler).GetEntriesAsync(Work, Monday, Monday.AddDays(1), CancellationToken.None);

        var request = Assert.Single(handler.Requests);
        Assert.Contains("/me/calendars/calendar-1/calendarView", request.Url, StringComparison.Ordinal);
        Assert.Contains("startDateTime=2026-10-05T00%3A00%3A00.0000000Z", request.Url, StringComparison.Ordinal);
        Assert.Contains("outlook.timezone=\"UTC\"", request.Prefer, StringComparison.Ordinal);
        Assert.Contains("outlook.body-content-type=\"html\"", request.Prefer, StringComparison.Ordinal);
    }

    private static readonly DateTimeOffset Monday = new(2026, 10, 5, 0, 0, 0, TimeSpan.Zero);

    private static readonly CalendarFeed Work = CalendarFeed.Create("Work", new MicrosoftSource("account-1", "ben@example.com", "calendar-1"), CalendarColor.Purple);

    private static GraphClient Client(FakeGraph handler) => new(new TokenSignIn(), new HttpClient(handler));

    private sealed class TokenSignIn : IMicrosoftSignIn
    {
        public Task<MicrosoftAccount?> SignInAsync(string? username, CancellationToken cancellationToken) => Task.FromResult<MicrosoftAccount?>(null);

        public Task<string> GetTokenAsync(string accountId, CancellationToken cancellationToken) => Task.FromResult($"token-for-{accountId}");
    }

    private sealed class FakeGraph(params string[] pages) : HttpMessageHandler
    {
        private int _next;

        public List<string> Authorizations { get; } = [];

        public List<(string Url, string Prefer)> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Authorizations.Add(request.Headers.Authorization?.ToString() ?? string.Empty);
            Requests.Add((request.RequestUri!.AbsoluteUri, string.Join(", ", request.Headers.TryGetValues("Prefer", out var prefer) ? prefer : [])));
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(pages[_next++], Encoding.UTF8, "application/json"),
            });
        }
    }
}
