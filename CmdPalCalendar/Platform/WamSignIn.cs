using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CmdPalCalendar.Graph;
using Microsoft.Identity.Client;
using Microsoft.Identity.Client.Broker;

namespace CmdPalCalendar.Platform;

internal sealed class WamSignIn : IMicrosoftSignIn
{
    // Microsoft Graph Command Line Tools, used until the extension has its own app registration.
    private const string ClientId = "14d82eec-204b-4c2f-b7e8-296a70dab67e";

    private static readonly string[] Scopes = ["Calendars.Read"];

    private readonly IPublicClientApplication _app;
    private IntPtr _parent;

    public WamSignIn(ProtectedTokenCache cache)
    {
        _app = PublicClientApplicationBuilder.Create(ClientId)
            .WithAuthority(AadAuthorityAudience.AzureAdAndPersonalMicrosoftAccount)
            .WithBroker(new BrokerOptions(BrokerOptions.OperatingSystems.Windows) { Title = "Calendar for Command Palette" })
            .WithParentActivityOrWindow(() => _parent = ForegroundWindow.Current())
            .Build();
        cache.Bind(_app.UserTokenCache);
    }

    public Task<MicrosoftAccount?> SignInAsync(string? username, CancellationToken cancellationToken)
    {
        var request = _app.AcquireTokenInteractive(Scopes);
        return InteractiveAsync(username is null ? request.WithPrompt(Prompt.SelectAccount) : request.WithLoginHint(username), cancellationToken);
    }

    public Task<MicrosoftAccount?> SignInAnotherAccountAsync(CancellationToken cancellationToken) =>
        InteractiveAsync(_app.AcquireTokenInteractive(Scopes).WithPrompt(Prompt.ForceLogin), cancellationToken);

    private async Task<MicrosoftAccount?> InteractiveAsync(AcquireTokenInteractiveParameterBuilder request, CancellationToken cancellationToken)
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
        finally
        {
            ForegroundWindow.Restore(_parent);
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
}
