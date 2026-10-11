using System;
using System.IO;

namespace CmdPalCalendar.Feeds;

internal abstract record FeedSource
{
    public abstract string Label { get; }

    public abstract string Provider { get; }
}

internal sealed record IcsSource(string Location) : FeedSource
{
    public override string Label => Describe(Location);

    public override string Provider => "calendar address";

    public static string Describe(string location)
    {
        if (Uri.TryCreate(location, UriKind.Absolute, out var uri) && !uri.IsFile)
        {
            return uri.Host;
        }

        return Path.GetFileName(location) is { Length: > 0 } file ? file : location;
    }
}

internal sealed record MicrosoftSource(string AccountId, string Account, string CalendarId) : FeedSource
{
    public override string Label => Account;

    public override string Provider => "Microsoft 365";
}
