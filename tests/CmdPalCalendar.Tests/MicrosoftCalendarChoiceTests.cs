using CmdPalCalendar.Feeds;
using CmdPalCalendar.Graph;

namespace CmdPalCalendar.Tests;

public sealed class MicrosoftCalendarChoiceTests : IDisposable
{
    private static readonly MicrosoftAccount Ben = new("account-1", "ben@example.com");
    private static readonly MicrosoftAccount Sam = new("account-2", "sam@example.com");
    private static readonly GraphCalendar Calendar = new("c1", "Calendar", null, true, CalendarColor.Teal);
    private static readonly GraphCalendar Birthdays = new("c2", "Birthdays", null, false, null);

    private readonly string _path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}", "calendars.json");
    private readonly CalendarFeedStore _store;
    private readonly FakeSignIn _signIn = new();
    private readonly FakeCalendars _graph = new();

    public MicrosoftCalendarChoiceTests() => _store = new CalendarFeedStore(_path, new PlainProtector());

    [Fact]
    public async Task Adding_the_first_account_offers_the_windows_account()
    {
        _signIn.Next = Ben;
        var choice = Choice(null);

        await choice.OpenAsync(CancellationToken.None);

        Assert.Equal(1, _signIn.SignIns);
        Assert.Equal(0, _signIn.AnotherAccountSignIns);
        Assert.Equal(Ben, choice.Account);
        Assert.Equal([Calendar, Birthdays], choice.Calendars);
    }

    [Fact]
    public async Task Adding_a_later_account_asks_for_a_fresh_sign_in()
    {
        Add(Ben, Calendar);
        _signIn.Next = Sam;
        var choice = Choice(null);

        await choice.OpenAsync(CancellationToken.None);

        Assert.Equal(1, _signIn.AnotherAccountSignIns);
        Assert.Equal(Sam, choice.Account);
    }

    [Fact]
    public async Task Choosing_for_a_known_account_skips_sign_in()
    {
        var choice = Choice(Ben);

        await choice.OpenAsync(CancellationToken.None);

        Assert.Equal(0, _signIn.SignIns + _signIn.AnotherAccountSignIns);
        Assert.NotNull(choice.Calendars);
    }

    [Fact]
    public async Task Reports_a_cancelled_sign_in()
    {
        var choice = Choice(null);

        await choice.OpenAsync(CancellationToken.None);

        Assert.Equal("Sign-in was cancelled", choice.Problem);
        Assert.Null(choice.Calendars);
        Assert.False(choice.IsBusy);
    }

    [Fact]
    public async Task Signs_in_again_by_username_when_calendars_need_it()
    {
        _graph.Failure = new SignInRequiredException(Ben.Id, "expired");
        var choice = Choice(Ben);
        await choice.OpenAsync(CancellationToken.None);
        Assert.True(choice.NeedsSignInAgain);

        _graph.Failure = null;
        _signIn.Next = Ben;
        await choice.RetryAsync(CancellationToken.None);

        Assert.Equal(["ben@example.com"], _signIn.Usernames);
        Assert.NotNull(choice.Calendars);
        Assert.Null(choice.Problem);
    }

    [Fact]
    public async Task Saving_adds_and_removes_to_match_the_selection()
    {
        Add(Ben, Calendar);
        var choice = Choice(Ben);
        await choice.OpenAsync(CancellationToken.None);

        var saved = choice.Save(new HashSet<string> { Birthdays.Id });

        Assert.Equal(new SavedChoice(1, 1), saved);
        var feed = Assert.Single(_store.Feeds);
        Assert.Equal(new MicrosoftSource(Ben.Id, Ben.Username, Birthdays.Id), feed.Source);
        Assert.Equal("Birthdays", feed.Name);
    }

    [Fact]
    public async Task New_calendars_take_their_outlook_colour()
    {
        var choice = Choice(Ben);
        await choice.OpenAsync(CancellationToken.None);

        choice.Save(new HashSet<string> { Calendar.Id });

        Assert.Equal(CalendarColor.Teal, Assert.Single(_store.Feeds).Color);
        Assert.True(choice.IsAdded(Calendar));
        Assert.False(choice.IsAdded(Birthdays));
    }

    [Fact]
    public async Task Saving_leaves_other_accounts_alone()
    {
        Add(Sam, Calendar);
        var choice = Choice(Ben);
        await choice.OpenAsync(CancellationToken.None);

        choice.Save(new HashSet<string> { Birthdays.Id });

        Assert.Equal(2, _store.Feeds.Count);
    }

    [Fact]
    public async Task Signing_out_removes_the_account_and_its_calendars()
    {
        Add(Ben, Calendar);
        var choice = Choice(Ben);

        await choice.SignOutAsync(CancellationToken.None);

        Assert.Empty(_store.Feeds);
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

    private void Add(MicrosoftAccount account, GraphCalendar calendar) =>
        _store.Add(CalendarFeed.Create(calendar.Name, new MicrosoftSource(account.Id, account.Username, calendar.Id), CalendarColor.Blue));

    private MicrosoftCalendarChoice Choice(MicrosoftAccount? account) =>
        new(_signIn, _graph, _store, new MicrosoftAccounts(_signIn, _store, TimeProvider.System), account);

    private sealed class FakeSignIn : IMicrosoftSignIn
    {
        public MicrosoftAccount? Next { get; set; }

        public int SignIns { get; private set; }

        public int AnotherAccountSignIns { get; private set; }

        public List<string?> Usernames { get; } = [];

        public List<string> SignedOut { get; } = [];

        public Task<MicrosoftAccount?> SignInAsync(string? username, CancellationToken cancellationToken)
        {
            SignIns++;
            Usernames.Add(username);
            return Task.FromResult(Next);
        }

        public Task<MicrosoftAccount?> SignInAnotherAccountAsync(CancellationToken cancellationToken)
        {
            AnotherAccountSignIns++;
            return Task.FromResult(Next);
        }

        public Task<string> GetTokenAsync(string accountId, CancellationToken cancellationToken) => Task.FromResult("token");

        public Task SignOutAsync(string accountId, CancellationToken cancellationToken)
        {
            SignedOut.Add(accountId);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<string>> GetSignedInAccountIdsAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<string>>([]);
    }

    private sealed class FakeCalendars : IGraphCalendars
    {
        public Exception? Failure { get; set; }

        public Task<IReadOnlyList<GraphCalendar>> GetCalendarsAsync(MicrosoftAccount account, CancellationToken cancellationToken) =>
            Failure is null
                ? Task.FromResult<IReadOnlyList<GraphCalendar>>([Calendar, Birthdays])
                : Task.FromException<IReadOnlyList<GraphCalendar>>(Failure);
    }
}
