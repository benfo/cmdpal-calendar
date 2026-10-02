using System;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using CmdPalCalendar.Feeds;
using CmdPalCalendar.Ics;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace CmdPalCalendar.Pages;

internal sealed partial class CalendarFeedFormPage : ContentPage
{
    private readonly CalendarFeedForm _form;

    public CalendarFeedFormPage(CalendarFeedStore store, IcsFeedCheck check, CalendarFeed? feed)
    {
        _form = new CalendarFeedForm(store, check, feed, busy => IsLoading = busy);
        Name = feed is null ? "Add" : "Edit";
        Title = feed is null ? "Add a calendar" : $"Edit {feed.Name}";
        Icon = new IconInfo(feed is null ? Glyphs.Add : Glyphs.Edit);
    }

    public override IContent[] GetContent() => [_form];

    private sealed record Fields(string Name, string Location, CalendarColor Color, bool Enabled);

    private sealed partial class CalendarFeedForm : FormContent
    {
        private static readonly TimeSpan CheckTimeout = TimeSpan.FromSeconds(20);

        private readonly CalendarFeedStore _store;
        private readonly IcsFeedCheck _check;
        private readonly CalendarFeed? _feed;
        private readonly Action<bool> _setBusy;
        private int _checking;

        public CalendarFeedForm(CalendarFeedStore store, IcsFeedCheck check, CalendarFeed? feed, Action<bool> setBusy)
        {
            _store = store;
            _check = check;
            _feed = feed;
            _setBusy = setBusy;
            TemplateJson = Template(feed is null ? "Add calendar" : "Save");
            Show(new Fields(feed?.Name ?? string.Empty, feed?.Location ?? string.Empty, feed?.Color ?? store.NextColor, feed?.Enabled ?? true));
        }

        public override ICommandResult SubmitForm(string inputs)
        {
            if (Interlocked.Exchange(ref _checking, 1) == 1)
            {
                return CommandResult.KeepOpen();
            }

            try
            {
                return Save(Read(inputs));
            }
            finally
            {
                Volatile.Write(ref _checking, 0);
            }
        }

        private CommandResult Save(Fields fields)
        {
            var addressChanged = _feed is null || !string.Equals(fields.Location, _feed.Location, StringComparison.Ordinal);
            var result = addressChanged ? Check(fields) : null;
            if (result is { IsValid: false })
            {
                Show(fields, error: result.Error!);
                return CommandResult.KeepOpen();
            }

            var name = fields.Name.Length > 0 ? fields.Name
                : addressChanged ? result?.CalendarName ?? CalendarFeed.Describe(fields.Location)
                : _feed!.Name;
            if (_feed is null)
            {
                _store.Add(CalendarFeed.Create(name, fields.Location, fields.Color) with { Enabled = fields.Enabled });
            }
            else
            {
                _store.Update(_feed with { Name = name, Location = fields.Location, Color = fields.Color, Enabled = fields.Enabled });
            }

            var verb = _feed is null ? "Added" : "Saved";
            return CommandResult.ShowToast(new ToastArgs { Message = $"{verb} {name}", Result = CommandResult.GoBack() });
        }

        private IcsFeedCheckResult Check(Fields fields)
        {
            Show(fields, status: "Checking the calendar…");
            _setBusy(true);
            try
            {
                using var timeout = new CancellationTokenSource(CheckTimeout);
                return Task.Run(() => _check.RunAsync(fields.Location, timeout.Token)).GetAwaiter().GetResult();
            }
            finally
            {
                _setBusy(false);
                Show(fields);
            }
        }

        private Fields Read(string inputs)
        {
            var values = JsonNode.Parse(inputs);
            return new Fields(
                Value(values, "name"),
                Value(values, "location"),
                Enum.TryParse<CalendarColor>(Value(values, "color"), out var color) ? color : _store.NextColor,
                Value(values, "enabled") != "false");
        }

        private static string Value(JsonNode? values, string key) => (values?[key]?.GetValue<string>() ?? string.Empty).Trim();

        private void Show(Fields fields, string error = "", string status = "") =>
            DataJson = new JsonObject
            {
                ["name"] = fields.Name,
                ["location"] = fields.Location,
                ["color"] = fields.Color.ToString(),
                ["enabled"] = fields.Enabled ? "true" : "false",
                ["error"] = error,
                ["status"] = status,
            }.ToJsonString();

        private static string Template(string submitTitle)
        {
            var colors = new JsonArray(Enum.GetNames<CalendarColor>()
                .Select(c => (JsonNode)new JsonObject { ["title"] = c, ["value"] = c })
                .ToArray());

            return $$"""
            {
                "type": "AdaptiveCard",
                "$schema": "http://adaptivecards.io/schemas/adaptive-card.json",
                "version": "1.5",
                "body": [
                    {
                        "type": "Input.Text",
                        "id": "location",
                        "label": "Calendar address",
                        "value": "${location}",
                        "placeholder": "https://… or webcal://… or C:\\path\\calendar.ics",
                        "isRequired": true,
                        "errorMessage": "Enter the calendar's address"
                    },
                    {
                        "type": "TextBlock",
                        "text": "In Outlook: Settings > Calendar > Shared calendars > Publish a calendar. In Google: your calendar's settings > Secret address in iCal format.",
                        "wrap": true,
                        "isSubtle": true,
                        "size": "Small"
                    },
                    {
                        "type": "Input.Text",
                        "id": "name",
                        "label": "Name",
                        "value": "${name}",
                        "placeholder": "Leave blank to use the calendar's own name"
                    },
                    {
                        "type": "Input.ChoiceSet",
                        "id": "color",
                        "label": "Colour",
                        "style": "compact",
                        "value": "${color}",
                        "choices": {{colors.ToJsonString()}}
                    },
                    {
                        "type": "Input.Toggle",
                        "id": "enabled",
                        "title": "Show this calendar",
                        "value": "${enabled}",
                        "valueOn": "true",
                        "valueOff": "false"
                    },
                    {
                        "type": "TextBlock",
                        "text": "${status}",
                        "$when": "${status != ''}",
                        "weight": "Bolder",
                        "wrap": true
                    },
                    {
                        "type": "TextBlock",
                        "text": "${error}",
                        "$when": "${error != ''}",
                        "color": "Attention",
                        "wrap": true
                    }
                ],
                "actions": [
                    {
                        "type": "Action.Submit",
                        "title": "{{submitTitle}}"
                    }
                ]
            }
            """;
        }
    }
}
