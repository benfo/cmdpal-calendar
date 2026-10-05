using System;
using System.Threading;
using System.Threading.Tasks;

namespace CmdPalCalendar.Graph;

internal sealed record MicrosoftAccount(string Id, string Username);

internal interface IMicrosoftSignIn
{
    Task<MicrosoftAccount?> SignInAsync(CancellationToken cancellationToken);

    Task<string> GetTokenAsync(string accountId, CancellationToken cancellationToken);
}

internal sealed class SignInRequiredException(string accountId)
    : Exception($"Sign in again to account {accountId}")
{
    public string AccountId { get; } = accountId;
}
