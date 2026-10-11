using System.Net;
using CmdPalCalendar.Events;
using CmdPalCalendar.Feeds;
using CmdPalCalendar.Graph;

namespace CmdPalCalendar.Tests;

public sealed class GraphEventReaderTests
{
    private const string DeltaLink = "https://graph.microsoft.com/v1.0/me/calendars/calendar-1/calendarView/delta?$deltatoken=t1";

    private static readonly DateTimeOffset Monday = new(2026, 10, 5, 0, 0, 0, TimeSpan.Zero);

    private static readonly CalendarFeed Work = CalendarFeed.Create("Work", new MicrosoftSource("account-1", "ben@example.com", "calendar-1"), CalendarColor.Purple);

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

        var entry = Assert.Single(await Reader(handler).GetEntriesAsync(Work, Monday, Monday.AddDays(1), CancellationToken.None));

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

        var entry = Assert.Single(await Reader(handler).GetEntriesAsync(Work, Monday, Monday.AddDays(1), CancellationToken.None));

        Assert.True(entry.IsAllDay);
        Assert.Equal(new DateTime(2026, 10, 5), entry.Start.DateTime);
        Assert.Equal(new DateTime(2026, 10, 6), entry.End.DateTime);
    }

    [Fact]
    public async Task Asks_for_the_calendar_view_in_utc_with_html_bodies()
    {
        var handler = new FakeGraph("""{ "value": [] }""");

        await Reader(handler).GetEntriesAsync(Work, Monday, Monday.AddDays(1), CancellationToken.None);

        var request = Assert.Single(handler.Requests);
        Assert.Contains("/me/calendars/calendar-1/calendarView", request.Url, StringComparison.Ordinal);
        Assert.Contains("startDateTime=2026-10-05T00%3A00%3A00.0000000Z", request.Url, StringComparison.Ordinal);
        Assert.Contains("outlook.timezone=\"UTC\"", request.Prefer, StringComparison.Ordinal);
        Assert.Contains("outlook.body-content-type=\"html\"", request.Prefer, StringComparison.Ordinal);
    }

    [Fact]
    public async Task First_sync_reads_every_page_of_the_window_and_keeps_the_delta_link()
    {
        var handler = new FakeGraph(
            $$"""{ "value": [ {{Event("e1", "Standup")}} ], "@odata.nextLink": "https://graph.microsoft.com/v1.0/me/calendars/calendar-1/calendarView/delta?$skiptoken=s1" }""",
            $$"""{ "value": [ {{Event("e2", "Review")}} ], "@odata.deltaLink": "{{DeltaLink}}" }""");

        var changes = await Reader(handler).SyncAsync(Work, Monday, Monday.AddDays(1), null, CancellationToken.None);

        Assert.True(changes.IsFull);
        Assert.Equal(["Standup", "Review"], changes.Changed.Select(e => e.Title));
        Assert.Equal(DeltaLink, changes.DeltaLink);
        Assert.Contains("/me/calendars/calendar-1/calendarView/delta", handler.Requests[0].Url, StringComparison.Ordinal);
        Assert.Contains("startDateTime=", handler.Requests[0].Url, StringComparison.Ordinal);
        Assert.Contains("odata.maxpagesize=100", handler.Requests[0].Prefer, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Later_syncs_follow_the_delta_link_and_report_removed_and_cancelled_events()
    {
        var handler = new FakeGraph(
            $$"""
            { "value": [
                {{Event("e1", "Standup (moved)")}},
                { "id": "e2", "@removed": { "reason": "deleted" } },
                { "id": "e3", "subject": "Off", "isCancelled": true,
                  "start": { "dateTime": "2026-10-05T09:00:00.0000000", "timeZone": "UTC" },
                  "end": { "dateTime": "2026-10-05T10:00:00.0000000", "timeZone": "UTC" } }
            ], "@odata.deltaLink": "{{DeltaLink}}2" }
            """);

        var changes = await Reader(handler).SyncAsync(Work, Monday, Monday.AddDays(1), DeltaLink, CancellationToken.None);

        Assert.False(changes.IsFull);
        Assert.Equal(DeltaLink, Assert.Single(handler.Requests).Url);
        Assert.Equal(["Standup (moved)"], changes.Changed.Select(e => e.Title));
        Assert.Equal(new HashSet<string> { "e2", "e3" }, changes.Removed);
        Assert.Equal(DeltaLink + "2", changes.DeltaLink);
    }

    [Fact]
    public async Task Starts_over_when_the_delta_link_has_expired()
    {
        var handler = new FakeGraph(
            (HttpStatusCode.Gone, """{ "error": { "code": "SyncStateNotFound", "message": "Expired" } }"""),
            (HttpStatusCode.OK, $$"""{ "value": [ {{Event("e1", "Standup")}} ], "@odata.deltaLink": "{{DeltaLink}}" }"""));

        var changes = await Reader(handler).SyncAsync(Work, Monday, Monday.AddDays(1), DeltaLink, CancellationToken.None);

        Assert.True(changes.IsFull);
        Assert.Equal(["Standup"], changes.Changed.Select(e => e.Title));
    }

    [Fact]
    public async Task Falls_back_to_the_calendar_view_when_delta_is_refused()
    {
        var handler = new FakeGraph(
            (HttpStatusCode.BadRequest, """{ "error": { "code": "ErrorInvalidRequest", "message": "Not supported" } }"""),
            (HttpStatusCode.OK, $$"""{ "value": [ {{Event("e1", "Standup")}} ] }"""));

        var changes = await Reader(handler).SyncAsync(Work, Monday, Monday.AddDays(1), null, CancellationToken.None);

        Assert.True(changes.IsFull);
        Assert.Null(changes.DeltaLink);
        Assert.Equal(["Standup"], changes.Changed.Select(e => e.Title));
    }

    [Fact]
    public async Task Fills_occurrences_from_their_series_master_and_leaves_the_master_out()
    {
        var handler = new FakeGraph(
            $$"""
            { "value": [
                {{Master("m1")}},
                {{Occurrence("o1", "m1", "2026-10-12")}},
                {{Occurrence("o2", "m1", "2026-10-26")}}
            ], "@odata.deltaLink": "{{DeltaLink}}" }
            """);

        var changes = await Reader(handler).SyncAsync(Work, Monday, Monday.AddDays(30), null, CancellationToken.None);

        Assert.Equal(["o1", "o2"], changes.Changed.Select(e => e.Uid));
        Assert.All(changes.Changed, e =>
        {
            Assert.Equal("Sprint review", e.Title);
            Assert.Equal("https://teams.microsoft.com/l/meetup-join/review", e.Link);
            Assert.Equal("Jane Doe", e.Organizer);
        });
        Assert.Equal(new DateTimeOffset(2026, 10, 12, 13, 0, 0, TimeSpan.Zero), changes.Changed[0].Start);
        Assert.Contains("m1", changes.Removed);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task Fetches_the_series_master_once_when_the_delta_has_only_occurrences()
    {
        var handler = new FakeGraph(
            $$"""
            { "value": [
                {{Occurrence("o1", "m1", "2026-10-12")}},
                {{Occurrence("o2", "m1", "2026-10-26")}}
            ], "@odata.deltaLink": "{{DeltaLink}}2" }
            """,
            Master("m1"));

        var changes = await Reader(handler).SyncAsync(Work, Monday, Monday.AddDays(30), DeltaLink, CancellationToken.None);

        Assert.Equal(["Sprint review", "Sprint review"], changes.Changed.Select(e => e.Title));
        Assert.Equal(2, handler.Requests.Count);
        Assert.Contains("/me/calendars/calendar-1/events/m1", handler.Requests[1].Url, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Keeps_an_occurrence_whose_series_master_is_gone()
    {
        var handler = new FakeGraph(
            (HttpStatusCode.OK, $$"""{ "value": [ {{Occurrence("o1", "m1", "2026-10-12")}} ], "@odata.deltaLink": "{{DeltaLink}}2" }"""),
            (HttpStatusCode.NotFound, """{ "error": { "code": "ErrorItemNotFound", "message": "Not found" } }"""));

        var changes = await Reader(handler).SyncAsync(Work, Monday, Monday.AddDays(30), DeltaLink, CancellationToken.None);

        Assert.Equal("(No title)", Assert.Single(changes.Changed).Title);
    }

    [Fact]
    public void Applies_changes_to_the_previous_entries()
    {
        IReadOnlyList<CalendarEntry> previous = [Entry("e1", "Standup"), Entry("e2", "Review"), Entry("e3", "Lunch")];
        var changes = new GraphChanges([Entry("e1", "Standup (moved)"), Entry("e4", "New")], new HashSet<string> { "e2" }, DeltaLink, IsFull: false);

        Assert.Equal(["Lunch", "Standup (moved)", "New"], changes.ApplyTo(previous).Select(e => e.Title));
        Assert.Equal(["Standup (moved)", "New"], (changes with { IsFull = true }).ApplyTo(previous).Select(e => e.Title));
    }

    private static string Event(string id, string subject) =>
        $$"""
        { "id": "{{id}}", "subject": "{{subject}}",
          "start": { "dateTime": "2026-10-05T07:00:00.0000000", "timeZone": "UTC" },
          "end": { "dateTime": "2026-10-05T07:15:00.0000000", "timeZone": "UTC" } }
        """;

    private static string Master(string id) =>
        $$"""
        { "id": "{{id}}", "type": "seriesMaster", "subject": "Sprint review",
          "start": { "dateTime": "2026-03-16T13:00:00.0000000", "timeZone": "UTC" },
          "end": { "dateTime": "2026-03-16T13:30:00.0000000", "timeZone": "UTC" },
          "body": { "contentType": "html", "content": "<p>Demo</p>" },
          "organizer": { "emailAddress": { "name": "Jane Doe", "address": "jane@example.com" } },
          "attendees": [ { "emailAddress": { "address": "sam@example.com" } } ],
          "onlineMeeting": { "joinUrl": "https://teams.microsoft.com/l/meetup-join/review" },
          "webLink": "https://outlook.office365.com/owa/?itemid={{id}}&exvsurl=1&path=/calendar/item" }
        """;

    private static string Occurrence(string id, string masterId, string date) =>
        $$"""
        { "id": "{{id}}", "type": "occurrence", "seriesMasterId": "{{masterId}}",
          "start": { "dateTime": "{{date}}T13:00:00.0000000", "timeZone": "UTC" },
          "end": { "dateTime": "{{date}}T13:30:00.0000000", "timeZone": "UTC" } }
        """;

    private static CalendarEntry Entry(string id, string title) =>
        new(id, title, Monday, Monday.AddMinutes(30), false, null, null, null, [], null, "Work");

    private static GraphEventReader Reader(FakeGraph handler) => new(handler.Connection());
}
