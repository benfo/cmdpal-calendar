using System.Net;
using System.Text;
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

    private static GraphClient Client(FakeGraph handler) => new(new TokenSignIn(), new HttpClient(handler));

    private sealed class TokenSignIn : IMicrosoftSignIn
    {
        public Task<MicrosoftAccount?> SignInAsync(CancellationToken cancellationToken) => Task.FromResult<MicrosoftAccount?>(null);

        public Task<string> GetTokenAsync(string accountId, CancellationToken cancellationToken) => Task.FromResult($"token-for-{accountId}");
    }

    private sealed class FakeGraph(params string[] pages) : HttpMessageHandler
    {
        private int _next;

        public List<string> Authorizations { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Authorizations.Add(request.Headers.Authorization?.ToString() ?? string.Empty);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(pages[_next++], Encoding.UTF8, "application/json"),
            });
        }
    }
}
