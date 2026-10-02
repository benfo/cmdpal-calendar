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

    private static readonly IconInfo CalendarIcon = new(Glyphs.Calendar);
    private static readonly IconInfo MeetingIcon = new(Glyphs.Video);
    private static readonly IconInfo ErrorIcon = new(Glyphs.Error);
    private static readonly IconInfo SettingsIcon = new(Glyphs.Settings);
    private static readonly IconInfo GoToIcon = new(Glyphs.CalendarDay);

    public ListItem Entry(CalendarEntry entry, DateTimeOffset? now)
    {
        var link = MeetingLink.Parse(entry.Link);
        ICommand command = link is null ? new NoOpCommand() : new JoinMeetingCommand(link);

        return new ListItem(command)
        {
            Title = entry.Title,
            Subtitle = Subtitle(entry, now),
            Icon = link is null or { Service: MeetingService.Other } ? CalendarIcon : MeetingIcon,
            Tags = link is not null && MeetingServiceText.Name(link.Service) is { } service ? [new Tag(service)] : [],
            Details = Details(entry),
            MoreCommands = [.. MeetingCommands(link, MeetingCredentials.Find(entry.Description)), .. navigation.Items],
        };
    }

    private static IContextItem[] MeetingCommands(MeetingLink? link, MeetingCredentials? credentials)
    {
        IContextItem[] browser = link?.App is null
            ? []
            : [new CommandContextItem(new OpenUrlCommand(link.Web.OriginalString) { Name = "Join in browser", Result = CommandResult.Dismiss() })];
        IContextItem[] copyLink = link is null ? [] : [Copy("Copy link", link.Web.OriginalString)];
        IContextItem[] copyCredentials = credentials is null ? [] : [Copy("Copy meeting ID and passcode", credentials.ToString())];

        IContextItem[] commands = [.. browser, .. copyLink, .. copyCredentials];
        return commands.Length == 0 ? [] : [.. commands, new Separator()];
    }

    private static CommandContextItem Copy(string name, string text) => new(new CopyTextCommand(text) { Name = name });

    public ListItem Message(string title, string subtitle = "", ICommand? command = null) =>
        new(command ?? new NoOpCommand())
        {
            Title = title,
            Subtitle = subtitle,
            Icon = CalendarIcon,
            MoreCommands = navigation.Items,
        };

    public ListItem PointToNext(string title, CalendarEntry? next, DateOnly today, Action<DateOnly> goTo)
    {
        if (next is null)
        {
            return Message(title, $"Nothing in the next {CalendarPage.LookAheadDays} days");
        }

        var date = DateOnly.FromDateTime(next.Start.DateTime);
        var time = next.IsAllDay ? "all day" : $"{next.Start:HH:mm}";
        var go = new AnonymousCommand(() => goTo(date)) { Name = $"Go to {DateText.Short(date)}", Result = CommandResult.KeepOpen() };

        return Message(title, $"Next: {DateText.Relative(date, today)} · {next.Title} {time}", go);
    }

    public ListItem GoTo(DateOnly date, int eventCount, Action go) =>
        new(new AnonymousCommand(go) { Name = "Go", Result = CommandResult.KeepOpen() })
        {
            Title = $"Go to {DateText.Long(date)}",
            Subtitle = eventCount switch { 0 => "No events", 1 => "1 event", _ => $"{eventCount} events" },
            Icon = GoToIcon,
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
