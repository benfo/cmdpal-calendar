using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CmdPalCalendar.Feeds;

namespace CmdPalCalendar.Graph;

internal sealed class MicrosoftAccounts(IMicrosoftSignIn signIn, CalendarFeedStore store)
{
    public bool IsLastOfItsAccount(CalendarFeed feed) =>
        feed.Source is MicrosoftSource source && store.Feeds.Count(f => IsOf(f, source.AccountId)) == 1;

    public int CalendarCount(string accountId) => store.Feeds.Count(f => IsOf(f, accountId));

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

    private static bool IsOf(CalendarFeed feed, string accountId) => feed.Source is MicrosoftSource s && s.AccountId == accountId;
}
