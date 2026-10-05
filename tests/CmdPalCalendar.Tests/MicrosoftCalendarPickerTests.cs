using CmdPalCalendar.Feeds;
using CmdPalCalendar.Graph;

namespace CmdPalCalendar.Tests;

public sealed class MicrosoftCalendarPickerTests : IDisposable
{
    private static readonly MicrosoftAccount Ben = new("account-1", "ben@example.com");
    private static readonly GraphCalendar Calendar = new("c1", "Calendar", null, true, CalendarColor.Teal);
    private static readonly GraphCalendar Birthdays = new("c2", "Birthdays", null, false, null);

    private readonly string _path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}", "calendars.json");
    private readonly CalendarFeedStore _store;
    private readonly FakeSignIn _signIn = new();
    private readonly FakeGraph _graph = new();

    public MicrosoftCalendarPickerTests() => _store = new CalendarFeedStore(_path, new PlainProtector());

    [Fact]
    public async Task Signs_in_when_no_account_is_known()
    {
        _signIn.Next = Ben;

        await Picker().OpenAsync(CancellationToken.None);

        Assert.Equal(1, _signIn.SignIns);
    }

    [Fact]
    public async Task Lists_the_calendars_of_accounts_already_added_without_signing_in()
    {
        _store.Add(CalendarFeed.Create("Calendar", new MicrosoftSource(Ben.Id, Ben.Username, Calendar.Id), CalendarColor.Blue));
        var picker = Picker();

        await picker.OpenAsync(CancellationToken.None);

        Assert.Equal(0, _signIn.SignIns);
        var account = Assert.Single(picker.Accounts);
        Assert.Equal([Calendar, Birthdays], account.Calendars);
    }

    [Fact]
    public async Task Reports_a_cancelled_sign_in()
    {
        var picker = Picker();

        await picker.SignInAsync(CancellationToken.None);

        Assert.Equal("Sign-in was cancelled", picker.SignInProblem);
        Assert.Empty(picker.Accounts);
        Assert.False(picker.IsSigningIn);
    }

    [Fact]
    public async Task Shows_an_error_for_an_account_whose_calendars_fail_to_load()
    {
        _signIn.Next = Ben;
        _graph.Failure = new SignInRequiredException(Ben.Id, "expired");
        var picker = Picker();

        await picker.SignInAsync(CancellationToken.None);

        var account = Assert.Single(picker.Accounts);
        Assert.Equal("Sign in again to see these calendars", account.Error);
        Assert.False(account.IsLoading);
    }

    [Fact]
    public void Adds_a_calendar_with_its_outlook_colour_and_marks_it_added()
    {
        var picker = Picker();

        var feed = picker.Add(Ben, Calendar);

        Assert.Equal(new MicrosoftSource(Ben.Id, Ben.Username, Calendar.Id), feed.Source);
        Assert.Equal(CalendarColor.Teal, feed.Color);
        Assert.Equal("Calendar", feed.Name);
        Assert.True(picker.IsAdded(Ben, Calendar));
        Assert.False(picker.IsAdded(Ben, Birthdays));
    }

    [Fact]
    public void Uses_the_next_colour_when_outlook_has_none()
    {
        var expected = _store.NextColor;

        Assert.Equal(expected, Picker().Add(Ben, Birthdays).Color);
    }

    public void Dispose()
    {
        var directory = Path.GetDirectoryName(_path)!;
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private MicrosoftCalendarPicker Picker() => new(_signIn, _graph, _store);

    private sealed class FakeSignIn : IMicrosoftSignIn
    {
        public MicrosoftAccount? Next { get; set; }

        public int SignIns { get; private set; }

        public Task<MicrosoftAccount?> SignInAsync(string? username, CancellationToken cancellationToken)
        {
            SignIns++;
            return Task.FromResult(Next);
        }

        public Task<string> GetTokenAsync(string accountId, CancellationToken cancellationToken) => Task.FromResult("token");
    }

    private sealed class FakeGraph : IGraphCalendars
    {
        public Exception? Failure { get; set; }

        public Task<IReadOnlyList<GraphCalendar>> GetCalendarsAsync(MicrosoftAccount account, CancellationToken cancellationToken) =>
            Failure is null
                ? Task.FromResult<IReadOnlyList<GraphCalendar>>([Calendar, Birthdays])
                : Task.FromException<IReadOnlyList<GraphCalendar>>(Failure);
    }
}
