using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using CmdPalCalendar.Events;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace CmdPalCalendar.Pages;

internal sealed class CalendarRows(NavigationCommands navigation, ICommand manage, LiveSubtitles live)
{
    private const int AttendeesShown = 8;

    private static readonly IconInfo CalendarIcon = new(Glyphs.Calendar);
    private static readonly IconInfo MeetingIcon = new(Glyphs.Video);
    private static readonly IconInfo ErrorIcon = new(Glyphs.Error);
    private static readonly IconInfo SettingsIcon = new(Glyphs.Settings);
    private static readonly IconInfo GoToIcon = new(Glyphs.CalendarDay);

    private readonly IContextItem[] _pageCommands = [.. navigation.Items, new CommandContextItem(manage)];

    public ListItem Entry(CalendarEntry entry, DateTimeOffset? now, bool showCalendar)
    {
        var link = MeetingLink.Parse(entry.Link);
        ICommand command = link is null ? new NoOpCommand() : new JoinMeetingCommand(link);

        var item = new ListItem(command)
        {
            Title = entry.IsAllDay ? entry.Title : $"{TimeText.Clock(entry.Start)}  {entry.Title}",
            Subtitle = Subtitle(entry, now),
            Icon = link is null or { Service: MeetingService.Other } ? CalendarIcon : MeetingIcon,
            Tags = Tags(entry, link, showCalendar),
            Details = Details(entry),
            MoreCommands = [.. MeetingCommands(link, MeetingCredentials.Find(entry.Description)), .. _pageCommands],
        };

        if (now is not null && !entry.IsAllDay)
        {
            live.Track(item, entry);
        }

        return item;
    }

    public void UpdateTimes(DateTimeOffset now) => live.Update(entry => Subtitle(entry, now));

    private static ITag[] Tags(CalendarEntry entry, MeetingLink? link, bool showCalendar)
    {
        ITag[] service = link is not null && MeetingServiceText.Name(link.Service) is { } name ? [new Tag(name)] : [];
        ITag[] calendar = showCalendar ? [CalendarColors.Tag(entry.Source, entry.Color)] : [];
        return [.. service, .. calendar];
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
            MoreCommands = _pageCommands,
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
            MoreCommands = _pageCommands,
        };

    public ListItem Problem(CalendarProblem problem, DateTimeOffset now) =>
        new(new NoOpCommand())
        {
            Title = problem.ShowingCopyFrom is null ? $"Couldn't load {problem.Calendar}" : $"Couldn't refresh {problem.Calendar}",
            Subtitle = problem.ShowingCopyFrom is { } copy
                ? $"{problem.Message} · showing data from {TimeText.Duration(now - copy)} ago"
                : problem.Message,
            Icon = ErrorIcon,
            MoreCommands = _pageCommands,
        };

    public ListItem NoCalendars(bool allTurnedOff) =>
        new(manage)
        {
            Title = allTurnedOff ? "All calendars are turned off" : "Add a calendar",
            Subtitle = allTurnedOff
                ? "Turn one on in Manage calendars"
                : "Add a published calendar (Outlook, Google, etc.) to see your events",
            Icon = SettingsIcon,
        };

    private static string Subtitle(CalendarEntry entry, DateTimeOffset? now)
    {
        if (entry.IsAllDay)
        {
            return entry.Location ?? entry.Source;
        }

        IEnumerable<string?> parts = [now is { } at ? Relative(entry, at) : null, entry.Location];
        return string.Join(" · ", parts.OfType<string>());
    }

    private static string Relative(CalendarEntry entry, DateTimeOffset now) =>
        entry.End <= now ? "ended"
        : entry.Start <= now ? $"started {TimeText.Duration(now - entry.Start)} ago"
        : $"in {TimeText.Duration(entry.Start - now)}";

    private static Details Details(CalendarEntry entry)
    {
        var body = new StringBuilder();
        body.AppendLine(entry.IsAllDay ? "**All day**" : $"**{TimeText.Range(entry)}**").AppendLine();
        AppendField(body, "Where", entry.Location);
        AppendField(body, "Organizer", entry.Organizer);
        AppendField(body, "Attendees", Attendees(entry.Attendees));

        if (entry.Notes is { } notes)
        {
            body.AppendLine().AppendLine(notes);
        }

        return new Details
        {
            Title = entry.Title,
            Body = body.ToString(),
            Metadata = [new DetailsElement { Key = "Calendar", Data = new DetailsTags { Tags = [CalendarColors.Tag(entry.Source, entry.Color)] } }],
        };
    }

    private static void AppendField(StringBuilder body, string label, string? value)
    {
        if (value is not null)
        {
            body.AppendLine(CultureInfo.CurrentCulture, $"**{label}:** {MarkdownText.Escape(value)}  ");
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
}
