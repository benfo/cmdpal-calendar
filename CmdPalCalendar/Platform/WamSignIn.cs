using System;
using System.Runtime.InteropServices;
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

    private readonly IPublicClientApplication _app = PublicClientApplicationBuilder.Create(ClientId)
        .WithAuthority(AadAuthorityAudience.AzureAdAndPersonalMicrosoftAccount)
        .WithBroker(new BrokerOptions(BrokerOptions.OperatingSystems.Windows) { Title = "Calendar for Command Palette" })
        .WithParentActivityOrWindow(NativeMethods.GetForegroundWindow)
        .Build();

    public async Task<MicrosoftAccount?> SignInAsync(CancellationToken cancellationToken)
    {
        try
        {
            var result = await _app.AcquireTokenInteractive(Scopes)
                .WithPrompt(Prompt.SelectAccount)
                .ExecuteAsync(cancellationToken);
            return new MicrosoftAccount(result.Account.HomeAccountId.Identifier, result.Account.Username);
        }
        catch (MsalClientException ex) when (ex.ErrorCode == MsalError.AuthenticationCanceledError)
        {
            return null;
        }
    }

    public async Task<string> GetTokenAsync(string accountId, CancellationToken cancellationToken)
    {
        var account = await _app.GetAccountAsync(accountId) ?? throw new SignInRequiredException(accountId);
        try
        {
            var result = await _app.AcquireTokenSilent(Scopes, account).ExecuteAsync(cancellationToken);
            return result.AccessToken;
        }
        catch (MsalUiRequiredException)
        {
            throw new SignInRequiredException(accountId);
        }
    }

    private static class NativeMethods
    {
        [DllImport("user32.dll")]
        public static extern IntPtr GetForegroundWindow();
    }
}
