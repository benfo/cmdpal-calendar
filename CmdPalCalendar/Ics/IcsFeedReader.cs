using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Ical.Net;

namespace CmdPalCalendar.Ics;

internal sealed class IcsFeedReader(HttpClient http)
{
    public IcsFeedReader()
        : this(CreateHttpClient())
    {
    }

    public static string DisplayName(string feed)
    {
        if (File.Exists(feed))
        {
            return Path.GetFileName(feed);
        }

        return Uri.TryCreate(feed, UriKind.Absolute, out var uri) ? uri.Host : feed;
    }

    public async Task<Calendar> ReadCalendarAsync(string feed, CancellationToken cancellationToken)
    {
        var text = await ReadAsync(feed, cancellationToken);
        return text.Contains("BEGIN:VCALENDAR", StringComparison.OrdinalIgnoreCase) && Calendar.Load(text) is { } calendar
            ? calendar
            : throw new InvalidDataException("That isn't an iCalendar (.ics) feed");
    }

    public async Task<string> ReadAsync(string feed, CancellationToken cancellationToken)
    {
        if (File.Exists(feed))
        {
            return await File.ReadAllTextAsync(feed, cancellationToken);
        }

        if (!Uri.TryCreate(feed, UriKind.Absolute, out var uri))
        {
            throw new ArgumentException("Not a URL or an existing file");
        }

        if (uri.IsFile)
        {
            return await File.ReadAllTextAsync(uri.LocalPath, cancellationToken);
        }

        using var response = await http.GetAsync(ToHttps(uri), cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(cancellationToken);
    }

    private static Uri ToHttps(Uri uri) =>
        uri.Scheme is "webcal" or "webcals"
            ? new UriBuilder(uri) { Scheme = Uri.UriSchemeHttps, Port = -1 }.Uri
            : uri;

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("CmdPalCalendar/0.1");
        return client;
    }
}
