using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using CmdPalCalendar.Graph;
using Microsoft.Identity.Client;
using Microsoft.Identity.Client.Broker;

namespace CmdPalCalendar.Platform;

internal sealed class WamSignIn : IMicrosoftSignIn
{
    // "Calendar for Command Palette" app registration: work and personal accounts, broker redirect, Calendars.Read.
    private const string ClientId = "2c35b16d-5b5c-427e-bb0b-d51aeafaa3e8";

    private static readonly string[] Scopes = ["Calendars.Read"];

    private readonly IPublicClientApplication _app = PublicClientApplicationBuilder.Create(ClientId)
        .WithAuthority(AadAuthorityAudience.AzureAdAndPersonalMicrosoftAccount)
        .WithBroker(new BrokerOptions(BrokerOptions.OperatingSystems.Windows) { Title = "Calendar for Command Palette" })
        .WithParentActivityOrWindow(NativeMethods.GetForegroundWindow)
        .Build();

    public WamSignIn(ProtectedTokenCache cache) => cache.Bind(_app.UserTokenCache);

    public Task<MicrosoftAccount?> SignInAsync(string? username, CancellationToken cancellationToken)
    {
        var request = _app.AcquireTokenInteractive(Scopes);
        return InteractiveAsync(username is null ? request.WithPrompt(Prompt.SelectAccount) : request.WithLoginHint(username), cancellationToken);
    }

    public Task<MicrosoftAccount?> SignInAnotherAccountAsync(CancellationToken cancellationToken) =>
        InteractiveAsync(_app.AcquireTokenInteractive(Scopes).WithPrompt(Prompt.ForceLogin), cancellationToken);

    private static async Task<MicrosoftAccount?> InteractiveAsync(AcquireTokenInteractiveParameterBuilder request, CancellationToken cancellationToken)
    {
        try
        {
            var result = await request.ExecuteAsync(cancellationToken);
            return new MicrosoftAccount(result.Account.HomeAccountId.Identifier, result.Account.Username);
        }
        catch (MsalClientException ex) when (ex.ErrorCode == MsalError.AuthenticationCanceledError)
        {
            return null;
        }
    }

    public async Task<string> GetTokenAsync(string accountId, CancellationToken cancellationToken)
    {
        var account = await _app.GetAccountAsync(accountId)
            ?? throw new SignInRequiredException(accountId, "Windows has no sign-in for this account");
        try
        {
            var result = await _app.AcquireTokenSilent(Scopes, account).ExecuteAsync(cancellationToken);
            return result.AccessToken;
        }
        catch (MsalUiRequiredException ex)
        {
            throw new SignInRequiredException(accountId, $"{ex.ErrorCode}: {FirstLine(ex.Message)}");
        }
    }

    public async Task SignOutAsync(string accountId, CancellationToken cancellationToken)
    {
        if (await _app.GetAccountAsync(accountId) is { } account)
        {
            await _app.RemoveAsync(account);
        }
    }

    public async Task<IReadOnlyList<string>> GetSignedInAccountIdsAsync(CancellationToken cancellationToken) =>
        (await _app.GetAccountsAsync()).Select(a => a.HomeAccountId.Identifier).ToList();

    private static string FirstLine(string text) => text.Split('\n', 2)[0].Trim();

    private static class NativeMethods
    {
        [DllImport("user32.dll")]
        public static extern IntPtr GetForegroundWindow();
    }
}
