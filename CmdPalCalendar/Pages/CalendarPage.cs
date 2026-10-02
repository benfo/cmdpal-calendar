using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CmdPalCalendar.Events;
using CmdPalCalendar.Ics;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Windows.System;

namespace CmdPalCalendar.Pages;

internal sealed partial class CalendarPage : ListPage
{
    private static readonly TimeSpan MaxAge = TimeSpan.FromMinutes(5);
    private static readonly IconInfo CalendarIcon = new("");
    private static readonly IconInfo LinkIcon = new("");
    private static readonly IconInfo RefreshIcon = new("");

    private readonly CalendarSettings _settings;
    private readonly Lock _lock = new();
    private CalendarLoadResult? _result;
    private Task? _loading;

    public CalendarPage(CalendarSettings settings)
    {
        _settings = settings;
        _settings.Settings.SettingsChanged += (_, _) => Refresh();

        Id = "CmdPalCalendar.Calendar";
        Icon = CalendarIcon;
        Title = "Today";
        Name = "Open";
        PlaceholderText = "Filter today's events";
        ShowDetails = true;
    }

    public override IListItem[] GetItems()
    {
        CalendarLoadResult? result;
        lock (_lock)
        {
            result = _result;
            if (result is null || DateTimeOffset.Now - result.LoadedAt > MaxAge || result.LoadedAt.Date != DateTime.Today)
            {
                StartLoad();
            }
        }

        if (_settings.IcsFeeds.Count == 0)
        {
            return [OpenSettingsItem()];
        }

        return result is null ? [] : BuildItems(result, DateTimeOffset.Now);
    }

    private void Refresh()
    {
        lock (_lock)
        {
            _result = null;
            StartLoad();
        }
    }

    private void StartLoad()
    {
        if (_loading is { IsCompleted: false } || _settings.IcsFeeds.Count == 0)
        {
            return;
        }

        IsLoading = true;
        _loading = Task.Run(async () =>
        {
            CalendarLoadResult loaded;
            try
            {
                loaded = await IcsCalendarSource.LoadTodayAsync(_settings.IcsFeeds, CancellationToken.None);
            }
            catch (Exception ex)
            {
                loaded = new CalendarLoadResult([], [ex.Message], DateTimeOffset.Now);
            }

            lock (_lock)
            {
                _result = loaded;
            }

            IsLoading = false;
            RaiseItemsChanged();
        });
    }

    private IListItem[] BuildItems(CalendarLoadResult result, DateTimeOffset now)
    {
        var timed = result.Entries.Where(e => !e.IsAllDay).ToList();

        var happeningNow = timed.Where(e => e.Start <= now && e.End > now).Select(e => ToListItem(e, now)).ToArray();
        var upNext = timed.Where(e => e.Start > now).Select(e => ToListItem(e, now)).ToArray();
        var allDay = result.Entries.Where(e => e.IsAllDay).Select(e => ToListItem(e, now)).ToArray();
        var earlier = timed.Where(e => e.End <= now).Select(e => ToListItem(e, now)).ToArray();

        IListItem[] items =
        [
            .. new Section("Happening now", happeningNow),
            .. new Section("Up next", upNext),
            .. new Section("All day", allDay),
            .. new Section("Earlier today", earlier),
            .. new Section("Problems", result.Errors.Select(ErrorItem).ToArray()),
        ];

        if (result.Entries.Count == 0 && result.Errors.Count == 0)
        {
            items = [new ListItem(new NoOpCommand()) { Title = "Nothing on your calendar today", Icon = CalendarIcon, MoreCommands = [RefreshContextItem()] }];
        }

        return items;
    }

    private ListItem ToListItem(CalendarEntry entry, DateTimeOffset now)
    {
        ICommand command = entry.Link is not null
            ? new OpenUrlCommand(entry.Link) { Name = "Open link", Icon = LinkIcon, Result = CommandResult.Dismiss() }
            : new NoOpCommand();

        var moreCommands = new List<IContextItem>();
        if (entry.Link is not null)
        {
            moreCommands.Add(new CommandContextItem(new CopyTextCommand(entry.Link) { Name = "Copy link" }));
        }

        moreCommands.Add(RefreshContextItem());

        return new ListItem(command)
        {
            Title = entry.Title,
            Subtitle = Subtitle(entry, now),
            Icon = CalendarIcon,
            Details = BuildDetails(entry),
            MoreCommands = [.. moreCommands],
        };
    }

    private static string Subtitle(CalendarEntry entry, DateTimeOffset now)
    {
        if (entry.IsAllDay)
        {
            return entry.Location ?? entry.Source;
        }

        var parts = new List<string> { $"{entry.Start:HH:mm}–{entry.End:HH:mm}", Relative(entry, now) };
        if (entry.Location is not null)
        {
            parts.Add(entry.Location);
        }

        return string.Join(" · ", parts);
    }

    private static string Relative(CalendarEntry entry, DateTimeOffset now)
    {
        if (entry.End <= now)
        {
            return "ended";
        }

        if (entry.Start <= now)
        {
            return $"started {Duration(now - entry.Start)} ago";
        }

        return $"in {Duration(entry.Start - now)}";
    }

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

        var hours = minutes / 60;
        var rest = minutes % 60;
        return rest == 0 ? $"{hours} h" : $"{hours} h {rest} min";
    }

    private static Details BuildDetails(CalendarEntry entry)
    {
        var body = new StringBuilder();
        body.AppendLine(entry.IsAllDay ? "**All day**" : $"**{entry.Start:HH:mm} – {entry.End:HH:mm}**");
        body.AppendLine();

        if (entry.Location is not null)
        {
            body.AppendLine(CultureInfo.CurrentCulture, $"**Where:** {Escape(entry.Location)}  ");
        }

        if (entry.Organizer is not null)
        {
            body.AppendLine(CultureInfo.CurrentCulture, $"**Organizer:** {Escape(entry.Organizer)}  ");
        }

        if (entry.Attendees.Count > 0)
        {
            const int shown = 8;
            var names = string.Join(", ", entry.Attendees.Take(shown).Select(Escape));
            var more = entry.Attendees.Count > shown ? $" +{entry.Attendees.Count - shown} more" : string.Empty;
            body.AppendLine(CultureInfo.CurrentCulture, $"**Attendees:** {names}{more}  ");
        }

        if (entry.Description is not null)
        {
            var lines = entry.Description
                .Split('\n')
                .Select(l => l.TrimEnd('\r'))
                .Where(l => !string.IsNullOrWhiteSpace(l))
                .Take(10);
            body.AppendLine();
            body.AppendLine(string.Join("  \n", lines.Select(Escape)));
        }

        return new Details
        {
            Title = entry.Title,
            Body = body.ToString(),
            Metadata =
            [
                new DetailsElement { Key = "Calendar", Data = new DetailsTags { Tags = [new Tag(entry.Source)] } },
            ],
        };
    }

    private static string Escape(string text)
    {
        var sb = new StringBuilder(text.Length);
        foreach (var c in text)
        {
            if ("\\`*_{}[]<>()#+-!|".Contains(c))
            {
                sb.Append('\\');
            }

            sb.Append(c);
        }

        return sb.ToString();
    }

    private ListItem ErrorItem(string error) =>
        new(new NoOpCommand())
        {
            Title = "Couldn't load a calendar",
            Subtitle = error,
            Icon = new IconInfo(""),
            MoreCommands = [RefreshContextItem()],
        };

    private ListItem OpenSettingsItem() =>
        new(_settings.Settings.SettingsPage)
        {
            Title = "Add an ICS feed in settings",
            Subtitle = "Paste a published calendar URL (Outlook, Google, etc.) to see today's events",
            Icon = new IconInfo(""),
        };

    private CommandContextItem RefreshContextItem() =>
        new(new AnonymousCommand(Refresh) { Name = "Refresh", Icon = RefreshIcon, Result = CommandResult.KeepOpen() })
        {
            RequestedShortcut = KeyChordHelpers.FromModifiers(ctrl: true, vkey: VirtualKey.R),
        };
}
