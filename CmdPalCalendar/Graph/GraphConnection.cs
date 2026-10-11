using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Graph;
using Microsoft.Kiota.Abstractions;
using Microsoft.Kiota.Abstractions.Authentication;

namespace CmdPalCalendar.Graph;

internal sealed class GraphConnection(IMicrosoftSignIn signIn, HttpClient http)
{
    public GraphServiceClient For(string accountId) =>
        new(http, new BaseBearerTokenAuthenticationProvider(new TokenProvider(signIn, accountId)));

    public static void PreferUtcAndHtml(RequestHeaders headers)
    {
        headers.Add("Prefer", "outlook.timezone=\"UTC\"");
        headers.Add("Prefer", "outlook.body-content-type=\"html\"");
    }

    private sealed class TokenProvider(IMicrosoftSignIn signIn, string accountId) : IAccessTokenProvider
    {
        public AllowedHostsValidator AllowedHostsValidator { get; } = new(["graph.microsoft.com"]);

        public Task<string> GetAuthorizationTokenAsync(Uri uri, Dictionary<string, object>? additionalAuthenticationContext = null, CancellationToken cancellationToken = default) =>
            signIn.GetTokenAsync(accountId, cancellationToken);
    }
}
