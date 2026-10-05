using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CmdPalCalendar.Feeds;

namespace CmdPalCalendar.Graph;

internal sealed record SavedChoice(int Added, int Removed);

internal sealed class MicrosoftCalendarChoice(
    IMicrosoftSignIn signIn,
    IGraphCalendars graph,
    CalendarFeedStore store,
    MicrosoftAccounts accounts,
    MicrosoftAccount? account)
{
    private int _busy;

    public event EventHandler? Changed;

    public MicrosoftAccount? Account { get; private set; } = account;

    public IReadOnlyList<GraphCalendar>? Calendars { get; private set; }

    public string? Problem { get; private set; }

    public bool IsBusy => Volatile.Read(ref _busy) == 1;

    public bool NeedsSignInAgain { get; private set; }

    public Task OpenAsync(CancellationToken cancellationToken) => RunAsync(LoadAsync, cancellationToken);

    public Task RetryAsync(CancellationToken cancellationToken) => RunAsync(
        async c =>
        {
            if (NeedsSignInAgain && Account is { } known)
            {
                Account = await signIn.SignInAsync(known.Username, c) ?? known;
            }

            await LoadAsync(c);
        },
        cancellationToken);

    public bool IsAdded(GraphCalendar calendar) => Account is { } a && store.Feeds.Any(f => IsCalendar(f, a.Id, calendar.Id));

    public SavedChoice Save(IReadOnlySet<string> selectedIds)
    {
        var current = Account ?? throw new InvalidOperationException("No account to save calendars for");
        var calendars = Calendars ?? [];
        var toAdd = calendars.Where(c => selectedIds.Contains(c.Id) && !IsAdded(c)).ToList();
        var toRemove = calendars.Where(c => !selectedIds.Contains(c.Id) && IsAdded(c)).Select(c => c.Id).ToHashSet();

        foreach (var calendar in toAdd)
        {
            store.Add(CalendarFeed.Create(calendar.Name, new MicrosoftSource(current.Id, current.Username, calendar.Id), calendar.Color ?? store.NextColor));
        }

        if (toRemove.Count > 0)
        {
            store.RemoveWhere(f => f.Source is MicrosoftSource s && s.AccountId == current.Id && toRemove.Contains(s.CalendarId));
        }

        return new SavedChoice(toAdd.Count, toRemove.Count);
    }

    public Task SignOutAsync(CancellationToken cancellationToken) =>
        Account is { } current ? accounts.SignOutAsync(current.Id, cancellationToken) : Task.CompletedTask;

    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        if (Account is null)
        {
            var isFirst = !store.Feeds.Any(f => f.Source is MicrosoftSource);
            Account = isFirst ? await signIn.SignInAsync(null, cancellationToken) : await signIn.SignInAnotherAccountAsync(cancellationToken);
            if (Account is null)
            {
                Problem = "Sign-in was cancelled";
                return;
            }
        }

        try
        {
            Calendars = await graph.GetCalendarsAsync(Account, cancellationToken);
            NeedsSignInAgain = false;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Problem = GraphErrors.Describe(ex);
            NeedsSignInAgain = ex is SignInRequiredException;
        }
    }

    private async Task RunAsync(Func<CancellationToken, Task> work, CancellationToken cancellationToken)
    {
        if (Interlocked.Exchange(ref _busy, 1) == 1)
        {
            return;
        }

        Problem = null;
        Changed?.Invoke(this, EventArgs.Empty);
        try
        {
            await work(cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Problem = GraphErrors.Describe(ex);
        }
        finally
        {
            Volatile.Write(ref _busy, 0);
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    private static bool IsCalendar(CalendarFeed feed, string accountId, string calendarId) =>
        feed.Source is MicrosoftSource s && s.AccountId == accountId && s.CalendarId == calendarId;
}
