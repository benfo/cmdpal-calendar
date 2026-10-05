using System;
using System.IO;

namespace CmdPalCalendar.Feeds;

internal abstract record FeedSource
{
    public abstract string Label { get; }
}

internal sealed record IcsSource(string Location) : FeedSource
{
    public override string Label => Describe(Location);

    public static string Describe(string location)
    {
        if (Uri.TryCreate(location, UriKind.Absolute, out var uri) && !uri.IsFile)
        {
            return uri.Host;
        }

        return Path.GetFileName(location) is { Length: > 0 } file ? file : location;
    }
}
