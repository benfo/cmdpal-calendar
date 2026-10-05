using System.Net;
using System.Text;
using CmdPalCalendar.Graph;

namespace CmdPalCalendar.Tests;

internal sealed class FakeGraph : HttpMessageHandler
{
    private readonly (HttpStatusCode Status, string Json)[] _responses;
    private int _next;

    public FakeGraph(params string[] pages) => _responses = pages.Select(p => (HttpStatusCode.OK, p)).ToArray();

    public FakeGraph(params (HttpStatusCode Status, string Json)[] responses) => _responses = responses;

    public List<string> Authorizations { get; } = [];

    public List<(string Url, string Prefer)> Requests { get; } = [];

    public GraphConnection Connection() => new(new TokenSignIn(), new HttpClient(this));

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Authorizations.Add(request.Headers.Authorization?.ToString() ?? string.Empty);
        Requests.Add((request.RequestUri!.AbsoluteUri, string.Join(", ", request.Headers.TryGetValues("Prefer", out var prefer) ? prefer : [])));
        var (status, json) = _responses[_next++];
        return Task.FromResult(new HttpResponseMessage(status)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        });
    }

    private sealed class TokenSignIn : IMicrosoftSignIn
    {
        public Task<MicrosoftAccount?> SignInAsync(string? username, CancellationToken cancellationToken) => Task.FromResult<MicrosoftAccount?>(null);

        public Task<string> GetTokenAsync(string accountId, CancellationToken cancellationToken) => Task.FromResult($"token-for-{accountId}");
    }
}
