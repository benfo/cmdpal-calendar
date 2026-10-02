using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using CmdPalCalendar.Events;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace CmdPalCalendar.Pages;

internal sealed class CalendarRows(NavigationCommands navigation)
{
    private const int AttendeesShown = 8;
    private const int DescriptionLinesShown = 10;
    private const string MarkdownSpecials = "\\`*_{}[]<>()#+-!|";

    private static readonly IconInfo CalendarIcon = new("");
    private static readonly IconInfo LinkIcon = new("");
    private static readonly IconInfo ErrorIcon = new("");
    private static readonly IconInfo SettingsIcon = new("");

    public ListItem Entry(CalendarEntry entry, DateTimeOffset? now)
    {
        ICommand command = entry.Link is { } link
            ? new OpenUrlCommand(link) { Name = "Open link", Icon = LinkIcon, Result = CommandResult.Dismiss() }
            : new NoOpCommand();

        IContextItem[] linkCommands = entry.Link is { } url
            ? [new CommandContextItem(new CopyTextCommand(url) { Name = "Copy link" }), new Separator()]
            : [];

        return new ListItem(command)
        {
            Title = entry.Title,
            Subtitle = Subtitle(entry, now),
            Icon = CalendarIcon,
            Details = Details(entry),
            MoreCommands = [.. linkCommands, .. navigation.Items],
        };
    }

    public ListItem Message(string title, string subtitle = "", ICommand? command = null) =>
        new(command ?? new NoOpCommand())
        {
            Title = title,
            Subtitle = subtitle,
            Icon = CalendarIcon,
            MoreCommands = navigation.Items,
        };

    public ListItem Error(string error) =>
        new(new NoOpCommand())
        {
            Title = "Couldn't load a calendar",
            Subtitle = error,
            Icon = ErrorIcon,
            MoreCommands = navigation.Items,
        };

    public static ListItem Settings(ICommand settingsPage) =>
        new(settingsPage)
        {
            Title = "Add an ICS feed in settings",
            Subtitle = "Paste a published calendar URL (Outlook, Google, etc.) to see your events",
            Icon = SettingsIcon,
        };

    private static string Subtitle(CalendarEntry entry, DateTimeOffset? now)
    {
        if (entry.IsAllDay)
        {
            return entry.Location ?? entry.Source;
        }

        IEnumerable<string?> parts = [TimeRange(entry), now is { } at ? Relative(entry, at) : null, entry.Location];
        return string.Join(" · ", parts.OfType<string>());
    }

    private static string TimeRange(CalendarEntry entry) => $"{entry.Start:HH:mm}–{entry.End:HH:mm}";

    private static string Relative(CalendarEntry entry, DateTimeOffset now) =>
        entry.End <= now ? "ended"
        : entry.Start <= now ? $"started {Duration(now - entry.Start)} ago"
        : $"in {Duration(entry.Start - now)}";

    private static string Duration(TimeSpan span)
    {
        var minutes = (int)Math.Round(span.TotalMinutes);
        if (minutes < 1)
        {
            return "less than a minute";
        }

        if (minutes < 60)
        {
            return $"{minutes} min";
        }

        var (hours, rest) = Math.DivRem(minutes, 60);
        return rest == 0 ? $"{hours} h" : $"{hours} h {rest} min";
    }

    private static Details Details(CalendarEntry entry)
    {
        var body = new StringBuilder();
        body.AppendLine(entry.IsAllDay ? "**All day**" : $"**{TimeRange(entry)}**").AppendLine();
        AppendField(body, "Where", entry.Location);
        AppendField(body, "Organizer", entry.Organizer);
        AppendField(body, "Attendees", Attendees(entry.Attendees));

        if (entry.Description is { } description)
        {
            var lines = description
                .Split('\n')
                .Select(l => l.TrimEnd('\r'))
                .Where(l => !string.IsNullOrWhiteSpace(l))
                .Take(DescriptionLinesShown)
                .Select(Escape);
            body.AppendLine().AppendLine(string.Join("  \n", lines));
        }

        return new Details
        {
            Title = entry.Title,
            Body = body.ToString(),
            Metadata = [new DetailsElement { Key = "Calendar", Data = new DetailsTags { Tags = [new Tag(entry.Source)] } }],
        };
    }

    private static void AppendField(StringBuilder body, string label, string? value)
    {
        if (value is not null)
        {
            body.AppendLine(CultureInfo.CurrentCulture, $"**{label}:** {Escape(value)}  ");
        }
    }

    private static string? Attendees(IReadOnlyList<string> attendees)
    {
        if (attendees.Count == 0)
        {
            return null;
        }

        var shown = string.Join(", ", attendees.Take(AttendeesShown));
        return attendees.Count > AttendeesShown ? $"{shown} +{attendees.Count - AttendeesShown} more" : shown;
    }

    private static string Escape(string text) =>
        string.Concat(text.Select(c => MarkdownSpecials.Contains(c) ? $"\\{c}" : c.ToString()));
}
