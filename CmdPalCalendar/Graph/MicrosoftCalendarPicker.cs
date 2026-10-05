using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CmdPalCalendar.Feeds;

namespace CmdPalCalendar.Graph;

internal sealed record PickerAccount(MicrosoftAccount Account, IReadOnlyList<GraphCalendar>? Calendars, string? Error)
{
    public bool IsLoading => Calendars is null && Error is null;
}

internal sealed class MicrosoftCalendarPicker(IMicrosoftSignIn signIn, IGraphCalendars graph, CalendarFeedStore store)
{
    private readonly Lock _lock = new();
    private IReadOnlyList<PickerAccount> _accounts = [];
    private int _signingIn;

    public event EventHandler? Changed;

    public IReadOnlyList<PickerAccount> Accounts => _accounts;

    public bool IsSigningIn => Volatile.Read(ref _signingIn) == 1;

    public string? SignInProblem { get; private set; }

    public Task OpenAsync(CancellationToken cancellationToken)
    {
        var known = KnownAccounts();
        return known.Count == 0
            ? SignInAsync(cancellationToken)
            : Task.WhenAll(known.Select(a => LoadAsync(a, cancellationToken)));
    }

    public async Task SignInAsync(CancellationToken cancellationToken)
    {
        if (Interlocked.Exchange(ref _signingIn, 1) == 1)
        {
            return;
        }

        SignInProblem = null;
        Changed?.Invoke(this, EventArgs.Empty);
        try
        {
            var account = await signIn.SignInAsync(null, cancellationToken);
            if (account is null)
            {
                SignInProblem = "Sign-in was cancelled";
                return;
            }

            await LoadAsync(account, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            SignInProblem = GraphErrors.Describe(ex);
        }
        finally
        {
            Volatile.Write(ref _signingIn, 0);
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    public bool IsAdded(MicrosoftAccount account, GraphCalendar calendar) =>
        store.Feeds.Any(f => f.Source is MicrosoftSource s && s.AccountId == account.Id && s.CalendarId == calendar.Id);

    public CalendarFeed Add(MicrosoftAccount account, GraphCalendar calendar)
    {
        var feed = CalendarFeed.Create(calendar.Name, new MicrosoftSource(account.Id, account.Username, calendar.Id), calendar.Color ?? store.NextColor);
        store.Add(feed);
        return feed;
    }

    private List<MicrosoftAccount> KnownAccounts() =>
        store.Feeds
            .Select(f => f.Source)
            .OfType<MicrosoftSource>()
            .Select(s => new MicrosoftAccount(s.AccountId, s.Account))
            .Concat(_accounts.Select(a => a.Account))
            .DistinctBy(a => a.Id)
            .ToList();

    private async Task LoadAsync(MicrosoftAccount account, CancellationToken cancellationToken)
    {
        Set(new PickerAccount(account, null, null));
        try
        {
            Set(new PickerAccount(account, await graph.GetCalendarsAsync(account, cancellationToken), null));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Set(new PickerAccount(account, null, GraphErrors.Describe(ex)));
        }
    }

    private void Set(PickerAccount account)
    {
        lock (_lock)
        {
            _accounts = _accounts.Any(a => a.Account.Id == account.Account.Id)
                ? _accounts.Select(a => a.Account.Id == account.Account.Id ? account : a).ToList()
                : [.. _accounts, account];
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }
}
