using CmdPalCalendar.Events;
using CmdPalCalendar.Feeds;
using CmdPalCalendar.Graph;
using CmdPalCalendar.Ics;

var failures = new List<string>();
void Expect(bool condition, string description)
{
    Console.WriteLine($"{(condition ? "ok  " : "FAIL")} {description}");
    if (!condition)
    {
        failures.Add(description);
    }
}

var feedPath = Path.Combine(AppContext.BaseDirectory, "rich.ics");
var reader = new IcsFeedReader();

var check = await new IcsFeedCheck(reader).RunAsync(feedPath, CancellationToken.None);
Expect(check.IsValid, $"feed check passes ({check.Error})");
Expect(check.CalendarName == "Trim check", "feed check reads the calendar name");

var cacheDirectory = Path.Combine(Path.GetTempPath(), $"trimcheck-{Guid.NewGuid():N}");
var feed = CalendarFeed.Create("Trim check", new IcsSource(feedPath), CalendarColor.Blue);
var source = new IcsCalendarSource(() => [feed], reader, new IcsFeedCache(cacheDirectory, new PlainProtector(), TimeProvider.System), TimeProvider.System);
await source.LoadAsync(CancellationToken.None);
Expect(source.Problems.Count == 0, $"feed loads without problems ({string.Join(" | ", source.Problems.Select(p => p.Message))})");

IReadOnlyList<string> TitlesOn(int month, int day)
{
    var date = new DateOnly(2026, month, day);
    return source.GetEntries(date, date.AddDays(1)).Select(e => e.Title).ToList();
}

var monday = TitlesOn(1, 5);
Expect(monday.Contains("Standup"), "recurring event with alarms and attendees");
Expect(monday.Contains("Vendor call"), "event with DURATION in another time zone");
Expect(monday.Contains("Public holiday"), "all-day monthly event");
Expect(!monday.Contains("Cancelled meeting"), "cancelled events are hidden");
Expect(TitlesOn(1, 6).Contains("Standup (moved)"), "moved occurrence (RECURRENCE-ID)");
Expect(!TitlesOn(1, 7).Contains("Standup"), "excluded date (EXDATE)");

var standup = source.GetEntries(new DateOnly(2026, 1, 5), new DateOnly(2026, 1, 6)).First(e => e.Title == "Standup");
Expect(standup.Notes?.Contains("**Daily**", StringComparison.Ordinal) == true, "HTML description becomes Markdown");
Expect(MeetingLink.Parse(standup.Link)?.Service == MeetingService.Teams, "Teams link is found");
Expect(MeetingCredentials.Find(standup.Description) is { MeetingId: not null }, "meeting ID is found");
Expect(DateQuery.TryParse("next fri", new DateOnly(2026, 1, 5), out _), "dates parse");

var graphFeed = CalendarFeed.Create("Work", new MicrosoftSource("account", "ben@example.com", "calendar"), CalendarColor.Blue);
var graph = new GraphClient(new FixedSignIn(), new HttpClient(new FixtureGraph(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "graph-events.json")))));
var graphEntries = await graph.GetEntriesAsync(graphFeed, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(1), CancellationToken.None);
Expect(graphEntries.Count == 2, "Graph events parse");
var graphStandup = graphEntries.FirstOrDefault(e => e.Title == "Standup");
Expect(graphStandup?.Link == "https://teams.microsoft.com/l/meetup-join/abc", "Graph join link is read");
Expect(graphStandup?.Notes?.Contains("**Daily**", StringComparison.Ordinal) == true, "Graph HTML body becomes Markdown");
Expect(graphStandup?.WebLink is not null && graphStandup.Organizer == "Jane Doe", "Graph web link and organizer are read");
Expect(graphEntries.Any(e => e.IsAllDay), "Graph all-day event");

Directory.Delete(cacheDirectory, recursive: true);
Console.WriteLine(failures.Count == 0 ? "Trimmed build works." : $"{failures.Count} check(s) failed.");
return failures.Count == 0 ? 0 : 1;

internal sealed class FixedSignIn : IMicrosoftSignIn
{
    public Task<MicrosoftAccount?> SignInAsync(CancellationToken cancellationToken) => Task.FromResult<MicrosoftAccount?>(null);

    public Task<string> GetTokenAsync(string accountId, CancellationToken cancellationToken) => Task.FromResult("token");
}

internal sealed class FixtureGraph(string json) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
        Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
        {
            Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json"),
        });
}

internal sealed class PlainProtector : ISecretProtector
{
    public byte[] Protect(byte[] data) => data;

    public byte[] Unprotect(byte[] data) => data;
}
