using System;
using System.Threading;
using System.Threading.Tasks;

namespace CmdPalCalendar.Graph;

internal sealed record MicrosoftAccount(string Id, string Username);

internal interface IMicrosoftSignIn
{
    Task<MicrosoftAccount?> SignInAsync(string? username, CancellationToken cancellationToken);

    Task<MicrosoftAccount?> SignInAnotherAccountAsync(CancellationToken cancellationToken);

    Task<string> GetTokenAsync(string accountId, CancellationToken cancellationToken);

    Task SignOutAsync(string accountId, CancellationToken cancellationToken);
}

internal sealed class SignInRequiredException(string accountId, string reason)
    : Exception(reason)
{
    public string AccountId { get; } = accountId;
}
