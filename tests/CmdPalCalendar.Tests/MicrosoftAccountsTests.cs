using CmdPalCalendar.Feeds;
using CmdPalCalendar.Graph;

namespace CmdPalCalendar.Tests;

public sealed class MicrosoftAccountsTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}", "calendars.json");
    private readonly CalendarFeedStore _store;
    private readonly RecordingSignIn _signIn = new();
    private readonly CalendarFeed _work = Feed("Work", "account-1");
    private readonly CalendarFeed _team = Feed("Team", "account-1");
    private readonly CalendarFeed _family = Feed("Family", "account-2");
    private readonly CalendarFeed _holidays = CalendarFeed.Create("Holidays", new IcsSource("https://example.com/holidays.ics"), CalendarColor.Green);

    public MicrosoftAccountsTests()
    {
        _store = new CalendarFeedStore(_path, new PlainProtector());
        foreach (var feed in new[] { _work, _team, _family, _holidays })
        {
            _store.Add(feed);
        }
    }

    [Fact]
    public async Task Removing_a_calendar_keeps_the_account_while_it_has_others()
    {
        var accounts = new MicrosoftAccounts(_signIn, _store);
        Assert.False(accounts.IsLastOfItsAccount(_work));

        await accounts.RemoveCalendarAsync(_work, CancellationToken.None);

        Assert.DoesNotContain(_store.Feeds, f => f.Id == _work.Id);
        Assert.Empty(_signIn.SignedOut);
    }

    [Fact]
    public async Task Removing_the_last_calendar_of_an_account_signs_it_out()
    {
        var accounts = new MicrosoftAccounts(_signIn, _store);
        Assert.True(accounts.IsLastOfItsAccount(_family));

        await accounts.RemoveCalendarAsync(_family, CancellationToken.None);

        Assert.Equal(["account-2"], _signIn.SignedOut);
    }

    [Fact]
    public async Task Removing_an_ics_calendar_signs_nothing_out()
    {
        await new MicrosoftAccounts(_signIn, _store).RemoveCalendarAsync(_holidays, CancellationToken.None);

        Assert.Equal(3, _store.Feeds.Count);
        Assert.Empty(_signIn.SignedOut);
    }

    [Fact]
    public async Task Signing_out_removes_every_calendar_of_that_account_only()
    {
        var accounts = new MicrosoftAccounts(_signIn, _store);
        Assert.Equal(2, accounts.CalendarCount("account-1"));

        await accounts.SignOutAsync("account-1", CancellationToken.None);

        Assert.Equal(["Family", "Holidays"], _store.Feeds.Select(f => f.Name));
        Assert.Equal(["account-1"], _signIn.SignedOut);
    }

    public void Dispose()
    {
        var directory = Path.GetDirectoryName(_path)!;
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static CalendarFeed Feed(string name, string accountId) =>
        CalendarFeed.Create(name, new MicrosoftSource(accountId, $"{accountId}@example.com", $"calendar-{name}"), CalendarColor.Blue);

    private sealed class RecordingSignIn : IMicrosoftSignIn
    {
        public List<string> SignedOut { get; } = [];

        public Task<MicrosoftAccount?> SignInAsync(string? username, CancellationToken cancellationToken) => Task.FromResult<MicrosoftAccount?>(null);

        public Task<MicrosoftAccount?> SignInAnotherAccountAsync(CancellationToken cancellationToken) => Task.FromResult<MicrosoftAccount?>(null);

        public Task<string> GetTokenAsync(string accountId, CancellationToken cancellationToken) => Task.FromResult("token");

        public Task SignOutAsync(string accountId, CancellationToken cancellationToken)
        {
            SignedOut.Add(accountId);
            return Task.CompletedTask;
        }
    }
}
