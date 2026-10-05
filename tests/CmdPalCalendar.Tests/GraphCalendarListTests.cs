using CmdPalCalendar.Feeds;
using CmdPalCalendar.Graph;

namespace CmdPalCalendar.Tests;

public sealed class GraphCalendarListTests
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

        var calendars = await List(handler).GetCalendarsAsync(Ben, CancellationToken.None);

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

        var calendars = await List(handler).GetCalendarsAsync(Ben, CancellationToken.None);

        Assert.Equal(["One", "Two"], calendars.Select(c => c.Name));
    }

    [Fact]
    public async Task Sends_the_account_token()
    {
        var handler = new FakeGraph("""{ "value": [] }""");

        await List(handler).GetCalendarsAsync(Ben, CancellationToken.None);

        Assert.Equal("Bearer token-for-account-1", Assert.Single(handler.Authorizations));
    }

    private static GraphCalendarList List(FakeGraph handler) => new(handler.Connection());
}
