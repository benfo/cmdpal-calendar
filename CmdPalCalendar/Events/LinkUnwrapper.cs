using System;
using System.Net;
using System.Web;

namespace CmdPalCalendar.Events;

internal static class LinkUnwrapper
{
    private const int MaxDepth = 5;

    public static string Unwrap(string url)
    {
        var current = WebUtility.HtmlDecode(url);
        for (var depth = 0; depth < MaxDepth && Inner(current) is { } inner; depth++)
        {
            current = inner;
        }

        return current;
    }

    private static string? Inner(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return null;
        }

        var host = uri.Host.ToLowerInvariant();
        if (host.EndsWith(".safelinks.protection.outlook.com", StringComparison.Ordinal))
        {
            return QueryValue(uri, "url");
        }

        if (IsGoogle(host) && uri.AbsolutePath == "/url")
        {
            return QueryValue(uri, "q") ?? QueryValue(uri, "url");
        }

        if (host == "urldefense.com" && uri.AbsolutePath.StartsWith("/v3/__", StringComparison.Ordinal))
        {
            var wrapped = url[(url.IndexOf("/v3/__", StringComparison.Ordinal) + "/v3/__".Length)..];
            var end = wrapped.IndexOf("__;", StringComparison.Ordinal);
            return end > 0 ? wrapped[..end] : null;
        }

        return null;
    }

    private static bool IsGoogle(string host) =>
        host.Split('.') is [.., "google", _] or [.., "google", _, _];

    private static string? QueryValue(Uri uri, string name) =>
        HttpUtility.ParseQueryString(uri.Query)[name] is { Length: > 0 } value ? value : null;
}
