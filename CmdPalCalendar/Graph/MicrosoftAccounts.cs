using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CmdPalCalendar.Feeds;

namespace CmdPalCalendar.Graph;

internal sealed class MicrosoftAccounts(IMicrosoftSignIn signIn, CalendarFeedStore store, TimeProvider time)
{
    private static readonly TimeSpan RecentSignIn = TimeSpan.FromMinutes(15);

    private readonly ConcurrentDictionary<string, DateTimeOffset> _signedInAt = new();

    public bool IsLastOfItsAccount(CalendarFeed feed) =>
        feed.Source is MicrosoftSource source && store.Feeds.Count(f => IsOf(f, source.AccountId)) == 1;

    public int CalendarCount(string accountId) => store.Feeds.Count(f => IsOf(f, accountId));

    public void SignedIn(string accountId) => _signedInAt[accountId] = time.GetUtcNow();

    public async Task RemoveCalendarAsync(CalendarFeed feed, CancellationToken cancellationToken)
    {
        var signOut = IsLastOfItsAccount(feed);
        store.Remove(feed.Id);
        if (signOut)
        {
            await signIn.SignOutAsync(((MicrosoftSource)feed.Source).AccountId, cancellationToken);
        }
    }

    public async Task SignOutAsync(string accountId, CancellationToken cancellationToken)
    {
        store.RemoveWhere(f => IsOf(f, accountId));
        await signIn.SignOutAsync(accountId, cancellationToken);
    }

    public async Task SignOutUnusedAsync(CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow();
        foreach (var accountId in await signIn.GetSignedInAccountIdsAsync(cancellationToken))
        {
            var recent = _signedInAt.TryGetValue(accountId, out var at) && now - at < RecentSignIn;
            if (!recent && CalendarCount(accountId) == 0)
            {
                await signIn.SignOutAsync(accountId, cancellationToken);
            }
        }
    }

    private static bool IsOf(CalendarFeed feed, string accountId) => feed.Source is MicrosoftSource s && s.AccountId == accountId;
}
